using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Egroo.Server.Database;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Egroo.Server.Test;

[TestClass]
public class EndpointIntegrationTest
{
    private const string JwtSecret = "super-secret-test-jwt-key-for-egroo-integration-tests-32+!";
    private const string EncryptionKey = "TestEncryptKey_32BytesLong!!!!!!";
    private const string EncryptionIV = "TestEncryptIV16!";

    private static readonly InMemoryDatabaseRoot DatabaseRoot = new();

    /// <summary>
    /// Creates a WebApplicationFactory that swaps PostgreSQL for InMemory.
    /// </summary>
    private static WebApplicationFactory<Program> CreateFactory(string dbName)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Secrets:Jwt"] = JwtSecret,
                        ["Encryption:Key"] = EncryptionKey,
                        ["Encryption:IV"] = EncryptionIV,
                        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=fake",
                    });
                });

                builder.ConfigureServices(services =>
                {
                    // Remove all EF Core DataContext-related registrations (Npgsql)
                    var toRemove = services.Where(d =>
                        d.ServiceType == typeof(DbContextOptions<DataContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        d.ServiceType == typeof(DataContext)).ToList();
                    foreach (var d in toRemove)
                        services.Remove(d);

                    // Register InMemory options directly (avoids dual-provider issue)
                    var options = new DbContextOptionsBuilder<DataContext>()
                        .UseInMemoryDatabase(dbName, DatabaseRoot)
                        .Options;

                    services.AddSingleton<DbContextOptions<DataContext>>(options);
                    services.AddSingleton<DbContextOptions>(options);
                    services.AddScoped<DataContext>();

                    // Override the JWT signing key (the app captures
                    // appsettings.json at registration time, so PostConfigure
                    // ensures the test key wins).
                    services.PostConfigure<JwtBearerOptions>(
                        JwtBearerDefaults.AuthenticationScheme,
                        opts =>
                        {
                            opts.TokenValidationParameters.IssuerSigningKey =
                                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
                        });
                });
            });
    }

    /// <summary>
    /// Generates a valid JWT for the given userId, matching the test JWT secret.
    /// </summary>
    private static string GenerateTestJwt(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "testuser"),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateTestJwt(userId));
        return client;
    }

    /// <summary>
    /// Signs up a new user and returns an HttpClient with the real Bearer token attached.
    /// </summary>
    private static async Task<HttpClient> SignUpAndGetAuthenticatedClientAsync(
        WebApplicationFactory<Program> factory, string? username = null)
    {
        username ??= $"u{Guid.NewGuid().ToString("N")[..10]}";
        var anonClient = factory.CreateClient();
        var signupResp = await anonClient.PostAsJsonAsync("/api/v1/Auth/signup", new
        {
            Username = username,
            Password = "StrongP@ss1"
        });
        signupResp.EnsureSuccessStatusCode();

        var rawBody = await signupResp.Content.ReadAsStringAsync();
        var json = JsonNode.Parse(rawBody)?.AsObject();
        var token = json?["token"]?.GetValue<string>();
        Assert.IsNotNull(token, $"Signup response missing token. Body: {rawBody}");

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ── Auth Endpoints ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task Auth_SignUp_ReturnsSuccess()
    {
        using var factory = CreateFactory($"auth_signup_{Guid.NewGuid():N}");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/Auth/signup", new
        {
            Username = $"u{Guid.NewGuid().ToString("N")[..10]}",
            Password = "StrongP@ss1"
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(body.Contains("\"success\":true"), $"Signup body: {body}");
    }

    [TestMethod]
    public async Task Auth_SignIn_AfterSignUp_ReturnsToken()
    {
        var username = $"u{Guid.NewGuid().ToString("N")[..10]}";
        using var factory = CreateFactory($"auth_signin_{Guid.NewGuid():N}");
        var client = factory.CreateClient();

        // Sign up first
        await client.PostAsJsonAsync("/api/v1/Auth/signup", new
        {
            Username = username,
            Password = "StrongP@ss1"
        });

        // Sign in
        var response = await client.PostAsJsonAsync("/api/v1/Auth/signin", new
        {
            Username = username,
            Password = "StrongP@ss1"
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task Auth_SignIn_WrongPassword_ReturnsNull()
    {
        var username = $"u{Guid.NewGuid().ToString("N")[..10]}";
        using var factory = CreateFactory($"auth_wrongpwd_{Guid.NewGuid():N}");
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/Auth/signup", new
        {
            Username = username,
            Password = "StrongP@ss1"
        });

        var response = await client.PostAsJsonAsync("/api/v1/Auth/signin", new
        {
            Username = username,
            Password = "WrongPassword"
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // Should return null result (no token)
        Assert.IsTrue(body == "null" || body.Contains("null"));
    }

    [TestMethod]
    public async Task Auth_RefreshSession_Unauthenticated_Returns401()
    {
        using var factory = CreateFactory($"auth_noauth_{Guid.NewGuid():N}");
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/Auth/refreshsession");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task Auth_ChangePassword_Unauthenticated_Returns401()
    {
        using var factory = CreateFactory($"auth_chgpwd_{Guid.NewGuid():N}");
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/v1/Auth/changepassword", new
        {
            OldPassword = "old",
            NewPassword = "new"
        });

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Agent Endpoints ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task Agent_GetUserAgents_Unauthenticated_Returns401()
    {
        using var factory = CreateFactory($"agent_noauth_{Guid.NewGuid():N}");
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/Agent/");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task Agent_CreateAndGetAgents_ReturnsAgent()
    {
        var dbName = $"agent_crud_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);

        // Create agent
        var createResp = await client.PostAsJsonAsync("/api/v1/Agent/", new
        {
            Name = "TestAgent",
            Description = "A test agent",
            Instructions = "Be helpful",
            Provider = 0,    // LlmProvider.OpenAI
            Model = "gpt-4o",
            IsPublished = false,
            AddPermission = 0, // AgentAddPermission.OwnerOnly
        });

        Assert.AreEqual(HttpStatusCode.OK, createResp.StatusCode);

        // Get agents
        var getResp = await client.GetAsync("/api/v1/Agent/");
        Assert.AreEqual(HttpStatusCode.OK, getResp.StatusCode);
    }

    [TestMethod]
    public async Task Agent_GetNonExistentAgent_Returns404()
    {
        var dbName = $"agent_404_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);

        var response = await client.GetAsync($"/api/v1/Agent/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Agent_DeleteNonExistentAgent_Returns404()
    {
        var dbName = $"agent_del_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);

        var response = await client.DeleteAsync($"/api/v1/Agent/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Agent_GetBuiltinTools_ReturnsOk()
    {
        var dbName = $"agent_builtin_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/v1/Agent/builtin-tools");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task Agent_GetFriends_ReturnsOk()
    {
        var dbName = $"agent_friends_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/v1/Agent/friends");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ── ChannelFile Endpoints ───────────────────────────────────────────────

    [TestMethod]
    public async Task ChannelFile_Upload_Unauthenticated_Returns401()
    {
        using var factory = CreateFactory($"chfile_noauth_{Guid.NewGuid():N}");
        var client = factory.CreateClient();

        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "file", "test.txt");

        var response = await client.PostAsync($"/api/v1/ChannelFiles/{Guid.NewGuid()}", content);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task ChannelFile_Download_Unauthenticated_Returns401()
    {
        using var factory = CreateFactory($"chfile_dl_{Guid.NewGuid():N}");
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/ChannelFiles/{Guid.NewGuid()}/token123/file.txt");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task ChannelFile_Upload_NotInChannel_ReturnsForbid()
    {
        var dbName = $"chfile_forbid_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);

        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "file", "test.txt");

        var response = await client.PostAsync($"/api/v1/ChannelFiles/{Guid.NewGuid()}", content);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Agent CRUD Extended ─────────────────────────────────────────────────

    [TestMethod]
    public async Task Agent_UpdateAgent_ReturnsOk()
    {
        var dbName = $"agent_upd_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);
        var agentId = await CreateAgentAsync(client);

        var response = await client.PutAsJsonAsync($"/api/v1/Agent/{agentId}", new
        {
            Name = "Updated",
            Provider = 0,
            Model = "gpt-4o-mini",
            IsActive = true,
            IsPublished = false,
            AddPermission = 0,
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task Agent_DeleteAgent_ReturnsOk()
    {
        var dbName = $"agent_deldel_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);
        var agentId = await CreateAgentAsync(client);

        var response = await client.DeleteAsync($"/api/v1/Agent/{agentId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task Agent_GetSpecificAgent_ReturnsOk()
    {
        var dbName = $"agent_getone_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);
        var agentId = await CreateAgentAsync(client);

        var response = await client.GetAsync($"/api/v1/Agent/{agentId}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Agent Skills ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Agent_Skills_CreateAndGet_ReturnsOk()
    {
        var dbName = $"skill_crud_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);
        var agentId = await CreateAgentAsync(client);

        var createResp = await client.PostAsJsonAsync($"/api/v1/Agent/{agentId}/skills", new
        {
            Name = "TestSkill",
            Path = "skills/test",
            IsEnabled = true,
        });
        Assert.AreEqual(HttpStatusCode.OK, createResp.StatusCode);

        var getResp = await client.GetAsync($"/api/v1/Agent/{agentId}/skills");
        Assert.AreEqual(HttpStatusCode.OK, getResp.StatusCode);
    }

    [TestMethod]
    public async Task Agent_Skills_CreateManaged_ReturnsOk()
    {
        var dbName = $"mskill_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);
        var agentId = await CreateAgentAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/Agent/{agentId}/skills/managed", new
        {
            Name = "MyManagedSkill",
            Content = "# Skill Instructions\nDo useful things.",
        });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Agent Knowledge ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task Agent_Knowledge_CRUD_ReturnsOk()
    {
        var dbName = $"know_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);
        var agentId = await CreateAgentAsync(client);

        // Create
        var createResp = await client.PostAsJsonAsync($"/api/v1/Agent/{agentId}/knowledge", new
        {
            Title = "Test Knowledge",
            Content = "Some content",
            IsEnabled = true,
        });
        Assert.AreEqual(HttpStatusCode.OK, createResp.StatusCode);
        var knowledgeId = (await createResp.Content.ReadFromJsonAsync<JsonObject>())?["id"]?.GetValue<Guid>();
        Assert.IsNotNull(knowledgeId);

        // Get
        var getResp = await client.GetAsync($"/api/v1/Agent/{agentId}/knowledge");
        Assert.AreEqual(HttpStatusCode.OK, getResp.StatusCode);

        // Update
        var updateResp = await client.PutAsJsonAsync($"/api/v1/Agent/knowledge/{knowledgeId}", new
        {
            Title = "Updated",
            Content = "Updated content",
            IsEnabled = false,
        });
        Assert.AreEqual(HttpStatusCode.OK, updateResp.StatusCode);

        // Delete
        var deleteResp = await client.DeleteAsync($"/api/v1/Agent/knowledge/{knowledgeId}");
        Assert.AreEqual(HttpStatusCode.OK, deleteResp.StatusCode);
    }

    // ── Agent Tools ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task Agent_Tools_CRUD_ReturnsOk()
    {
        var dbName = $"tools_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);
        var agentId = await CreateAgentAsync(client);

        // Create
        var createResp = await client.PostAsJsonAsync($"/api/v1/Agent/{agentId}/tools", new
        {
            Name = "TestTool",
            Description = "A tool",
            IsEnabled = true,
            Source = 0,
        });
        Assert.AreEqual(HttpStatusCode.OK, createResp.StatusCode);
        var toolId = (await createResp.Content.ReadFromJsonAsync<JsonObject>())?["id"]?.GetValue<Guid>();
        Assert.IsNotNull(toolId);

        // Get
        var getResp = await client.GetAsync($"/api/v1/Agent/{agentId}/tools");
        Assert.AreEqual(HttpStatusCode.OK, getResp.StatusCode);

        // Update
        var updateResp = await client.PutAsJsonAsync($"/api/v1/Agent/tools/{toolId}", new
        {
            Name = "Updated",
            Description = "Updated desc",
            IsEnabled = false,
        });
        Assert.AreEqual(HttpStatusCode.OK, updateResp.StatusCode);

        // Delete
        var deleteResp = await client.DeleteAsync($"/api/v1/Agent/tools/{toolId}");
        Assert.AreEqual(HttpStatusCode.OK, deleteResp.StatusCode);
    }

    [TestMethod]
    public async Task Agent_SeedBuiltinTools_ReturnsOk()
    {
        var dbName = $"seed_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);
        var agentId = await CreateAgentAsync(client);

        var response = await client.PostAsync($"/api/v1/Agent/{agentId}/seed-builtin-tools", null);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Agent MCP Servers ───────────────────────────────────────────────────

    [TestMethod]
    public async Task Agent_McpServers_CRUD_ReturnsOk()
    {
        var dbName = $"mcp_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);
        var agentId = await CreateAgentAsync(client);

        // Create
        var createResp = await client.PostAsJsonAsync($"/api/v1/Agent/{agentId}/mcp-servers", new
        {
            Name = "TestServer",
            Endpoint = "http://localhost:5000",
        });
        Assert.AreEqual(HttpStatusCode.OK, createResp.StatusCode);
        var serverId = (await createResp.Content.ReadFromJsonAsync<JsonObject>())?["id"]?.GetValue<Guid>();
        Assert.IsNotNull(serverId);

        // Get
        var getResp = await client.GetAsync($"/api/v1/Agent/{agentId}/mcp-servers");
        Assert.AreEqual(HttpStatusCode.OK, getResp.StatusCode);

        // Update
        var updateResp = await client.PutAsJsonAsync($"/api/v1/Agent/mcp-servers/{serverId}", new
        {
            Name = "Updated",
            Endpoint = "http://localhost:6000",
            IsActive = false,
        });
        Assert.AreEqual(HttpStatusCode.OK, updateResp.StatusCode);

        // Delete
        var deleteResp = await client.DeleteAsync($"/api/v1/Agent/mcp-servers/{serverId}");
        Assert.AreEqual(HttpStatusCode.OK, deleteResp.StatusCode);
    }

    // ── Agent Conversations ─────────────────────────────────────────────────

    [TestMethod]
    public async Task Agent_Conversations_CRUD_ReturnsOk()
    {
        var dbName = $"conv_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);
        var agentId = await CreateAgentAsync(client);

        // Create
        var createResp = await client.PostAsJsonAsync($"/api/v1/Agent/{agentId}/conversations", new
        {
            Title = "Test Conversation",
        });
        Assert.AreEqual(HttpStatusCode.OK, createResp.StatusCode);
        var convId = (await createResp.Content.ReadFromJsonAsync<JsonObject>())?["id"]?.GetValue<Guid>();
        Assert.IsNotNull(convId);

        // Get all for agent
        var getAllResp = await client.GetAsync($"/api/v1/Agent/{agentId}/conversations");
        Assert.AreEqual(HttpStatusCode.OK, getAllResp.StatusCode);

        // Get specific
        var getResp = await client.GetAsync($"/api/v1/Agent/conversations/{convId}");
        Assert.AreEqual(HttpStatusCode.OK, getResp.StatusCode);

        // Get messages
        var msgResp = await client.GetAsync($"/api/v1/Agent/conversations/{convId}/messages?skip=0&take=10");
        Assert.AreEqual(HttpStatusCode.OK, msgResp.StatusCode);

        // Delete
        var deleteResp = await client.DeleteAsync($"/api/v1/Agent/conversations/{convId}");
        Assert.AreEqual(HttpStatusCode.OK, deleteResp.StatusCode);
    }

    [TestMethod]
    public async Task Agent_GetNonExistentConversation_Returns404()
    {
        var dbName = $"conv404_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);

        var response = await client.GetAsync($"/api/v1/Agent/conversations/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Agent Publishing ────────────────────────────────────────────────────

    [TestMethod]
    public async Task Agent_PublishAndUnpublish_ReturnsOk()
    {
        var dbName = $"publish_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);
        var agentId = await CreateAgentAsync(client);

        // Publish
        var pubResp = await client.PostAsync($"/api/v1/Agent/{agentId}/publish", null);
        Assert.AreEqual(HttpStatusCode.OK, pubResp.StatusCode);

        // Search published (anonymous)
        var anonClient = factory.CreateClient();
        var searchResp = await anonClient.GetAsync("/api/v1/Agent/published/search?query=Test&maxResults=5");
        Assert.AreEqual(HttpStatusCode.OK, searchResp.StatusCode);

        // Get published details (anonymous - uses OwnerAndOthers check, so use authenticated client)
        var detailResp = await client.GetAsync($"/api/v1/Agent/published/{agentId}");
        Assert.AreEqual(HttpStatusCode.OK, detailResp.StatusCode);

        // Unpublish
        var unpubResp = await client.PostAsync($"/api/v1/Agent/{agentId}/unpublish", null);
        Assert.AreEqual(HttpStatusCode.OK, unpubResp.StatusCode);
    }

    // ── Agent Friends ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task Agent_Friends_AddCheckRemove_ReturnsOk()
    {
        var dbName = $"afriend_{Guid.NewGuid():N}";
        using var factory = CreateFactory(dbName);
        var client = await SignUpAndGetAuthenticatedClientAsync(factory);
        var agentId = await CreateAgentAsync(client);

        // Publish agent first (required to add as friend)
        await client.PostAsync($"/api/v1/Agent/{agentId}/publish", null);

        // Add friend
        var addResp = await client.PostAsync($"/api/v1/Agent/friends/{agentId}", null);
        Assert.AreEqual(HttpStatusCode.OK, addResp.StatusCode);

        // Check
        var checkResp = await client.GetAsync($"/api/v1/Agent/friends/{agentId}/check");
        Assert.AreEqual(HttpStatusCode.OK, checkResp.StatusCode);

        // Remove
        var removeResp = await client.DeleteAsync($"/api/v1/Agent/friends/{agentId}");
        Assert.AreEqual(HttpStatusCode.OK, removeResp.StatusCode);
    }

    // ── Agent Endpoint helper ───────────────────────────────────────────────

    private static async Task<Guid> CreateAgentAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/Agent/", new
        {
            Name = "TestAgent",
            Description = "Desc",
            Instructions = "Be helpful",
            Provider = 0,
            Model = "gpt-4o",
            IsPublished = false,
            AddPermission = 0,
        });
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        var id = json?["id"]?.GetValue<Guid>();
        Assert.IsNotNull(id, $"Failed to create agent: {json}");
        return id.Value;
    }
}
