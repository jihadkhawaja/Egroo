using Egroo.Server.Security;
using Egroo.Server.Services;
using Egroo.Server.Tools;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Egroo.Server.API
{
    public static class AgentEndpoint
    {
        public static void MapAgents(this IEndpointRouteBuilder routes)
        {
            var group = routes.MapGroup("/api/v1/Agent")
                .WithTags("Agent")
                .RequireRateLimiting("Api")
                .RequireAuthorization();

            // ── Agent CRUD ───────────────────────────────────────────────

            group.MapPost("/", async (IAgentRepository repo, CreateAgentRequest req) =>
            {
                var definition = new AgentDefinition
                {
                    Name = req.Name,
                    Description = req.Description,
                    Instructions = req.Instructions,
                    Provider = req.Provider,
                    Model = req.Model,
                    ApiKey = req.ApiKey,
                    Endpoint = req.Endpoint,
                    IsPublished = req.IsPublished,
                    AddPermission = req.AddPermission,
                    Temperature = req.Temperature,
                    MaxTokens = req.MaxTokens,
                    SkillsInstructionPrompt = req.SkillsInstructionPrompt
                };

                var result = await repo.CreateAgent(definition);
                if (result is null)
                {
                    return Results.BadRequest(new { error = "Failed to create agent." });
                }

                StripSecrets(result);
                return Results.Ok(result);
            });

            group.MapGet("/", async (IAgentRepository repo) =>
            {
                var agents = await repo.GetUserAgents();
                if (agents is not null)
                {
                    foreach (var a in agents)
                    {
                        StripSecrets(a);
                    }
                }
                return Results.Ok(agents);
            });

            group.MapGet("/{agentId:guid}", async (IAgentRepository repo, Guid agentId) =>
            {
                var agent = await repo.GetAgent(agentId);
                if (agent is null)
                {
                    return Results.NotFound();
                }
                StripSecrets(agent);
                return Results.Ok(agent);
            });

            group.MapPut("/{agentId:guid}", async (IAgentRepository repo, Guid agentId, UpdateAgentRequest req) =>
            {
                var definition = new AgentDefinition
                {
                    Id = agentId,
                    Name = req.Name,
                    Description = req.Description,
                    Instructions = req.Instructions,
                    Provider = req.Provider,
                    Model = req.Model,
                    ApiKey = req.ApiKey,
                    Endpoint = req.Endpoint,
                    IsActive = req.IsActive,
                    IsPublished = req.IsPublished,
                    AddPermission = req.AddPermission,
                    Temperature = req.Temperature,
                    MaxTokens = req.MaxTokens,
                    SkillsInstructionPrompt = req.SkillsInstructionPrompt
                };

                var success = await repo.UpdateAgent(definition);
                return success ? Results.Ok() : Results.BadRequest(new { error = "Failed to update agent." });
            });

            group.MapDelete("/{agentId:guid}", async (IAgentRepository repo, Guid agentId) =>
            {
                var success = await repo.DeleteAgent(agentId);
                return success ? Results.Ok() : Results.NotFound();
            });

            // ── Agent Skills ────────────────────────────────────────────

            group.MapPost("/{agentId:guid}/skills", async (IAgentRepository repo, Guid agentId, CreateSkillDirectoryRequest req) =>
            {
                var skillDirectory = new AgentSkillDirectory
                {
                    AgentDefinitionId = agentId,
                    Name = ResolveSkillDirectoryName(req.Name, req.Path),
                    Path = req.Path,
                    IsEnabled = req.IsEnabled
                };

                var result = await repo.AddSkillDirectory(skillDirectory);
                return result is not null ? Results.Ok(result) : Results.BadRequest(new { error = "Failed to add skill directory." });
            });

            group.MapPost("/{agentId:guid}/skills/managed", async (
                IAgentRepository repo,
                AgentManagedSkillsService managedSkills,
                Guid agentId,
                CreateManagedSkillRequest req) =>
            {
                try
                {
                    var managedSkill = managedSkills.CreateManagedSkill(agentId, req.Name, req.Content, req.FileName);
                    var skillDirectory = new AgentSkillDirectory
                    {
                        AgentDefinitionId = agentId,
                        Name = managedSkill.DisplayName,
                        Path = managedSkill.RelativeDirectoryPath,
                        IsEnabled = req.IsEnabled
                    };

                    var result = await repo.AddSkillDirectory(skillDirectory);
                    if (result is not null)
                    {
                        return Results.Ok(result);
                    }

                    managedSkills.DeleteManagedSkillDirectory(managedSkill.RelativeDirectoryPath);
                    return Results.BadRequest(new { error = "Failed to add managed skill." });
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            });

            group.MapGet("/{agentId:guid}/skills", async (IAgentRepository repo, Guid agentId) =>
            {
                var directories = await repo.GetAgentSkillDirectories(agentId);
                return Results.Ok(directories);
            });

            group.MapPut("/skills/{skillDirectoryId:guid}", async (IAgentRepository repo, Guid skillDirectoryId, UpdateSkillDirectoryRequest req) =>
            {
                var skillDirectory = new AgentSkillDirectory
                {
                    Id = skillDirectoryId,
                    Name = ResolveSkillDirectoryName(req.Name, req.Path),
                    Path = req.Path,
                    IsEnabled = req.IsEnabled
                };

                var success = await repo.UpdateSkillDirectory(skillDirectory);
                return success ? Results.Ok() : Results.BadRequest(new { error = "Failed to update skill directory." });
            });

            group.MapDelete("/skills/{skillDirectoryId:guid}", async (
                IAgentRepository repo,
                AgentManagedSkillsService managedSkills,
                Guid skillDirectoryId) =>
            {
                var skillDirectory = await repo.GetSkillDirectory(skillDirectoryId);
                if (skillDirectory is null)
                {
                    return Results.NotFound();
                }

                var success = await repo.DeleteSkillDirectory(skillDirectoryId);
                if (success && managedSkills.IsManagedSkillPath(skillDirectory.Path))
                {
                    managedSkills.DeleteManagedSkillDirectory(skillDirectory.Path);
                }

                return success ? Results.Ok() : Results.NotFound();
            });

            // ── Knowledge ────────────────────────────────────────────────

            group.MapPost("/{agentId:guid}/knowledge", async (IAgentRepository repo, Guid agentId, CreateKnowledgeRequest req) =>
            {
                var knowledge = new AgentKnowledge
                {
                    AgentDefinitionId = agentId,
                    Title = req.Title,
                    Content = req.Content,
                    IsEnabled = req.IsEnabled
                };

                var result = await repo.AddKnowledge(knowledge);
                return result is not null ? Results.Ok(result) : Results.BadRequest(new { error = "Failed to add knowledge." });
            });

            group.MapGet("/{agentId:guid}/knowledge", async (IAgentRepository repo, Guid agentId) =>
            {
                var items = await repo.GetAgentKnowledge(agentId);
                return Results.Ok(items);
            });

            group.MapPut("/knowledge/{knowledgeId:guid}", async (IAgentRepository repo, Guid knowledgeId, UpdateKnowledgeRequest req) =>
            {
                var knowledge = new AgentKnowledge
                {
                    Id = knowledgeId,
                    Title = req.Title,
                    Content = req.Content,
                    IsEnabled = req.IsEnabled
                };

                var success = await repo.UpdateKnowledge(knowledge);
                return success ? Results.Ok() : Results.BadRequest(new { error = "Failed to update knowledge." });
            });

            group.MapDelete("/knowledge/{knowledgeId:guid}", async (IAgentRepository repo, Guid knowledgeId) =>
            {
                var success = await repo.DeleteKnowledge(knowledgeId);
                return success ? Results.Ok() : Results.NotFound();
            });

            // ── Tools ────────────────────────────────────────────────────

            group.MapPost("/{agentId:guid}/tools", async (IAgentRepository repo, Guid agentId, CreateToolRequest req) =>
            {
                var tool = new AgentTool
                {
                    AgentDefinitionId = agentId,
                    Name = req.Name,
                    Description = req.Description,
                    ParametersSchema = req.ParametersSchema,
                    IsEnabled = req.IsEnabled,
                    Source = req.Source,
                    McpServerId = req.McpServerId
                };

                var result = await repo.AddTool(tool);
                return result is not null ? Results.Ok(result) : Results.BadRequest(new { error = "Failed to add tool." });
            });

            group.MapGet("/{agentId:guid}/tools", async (IAgentRepository repo, Guid agentId) =>
            {
                var tools = await repo.GetAgentTools(agentId);
                return Results.Ok(tools);
            });

            group.MapPut("/tools/{toolId:guid}", async (IAgentRepository repo, Guid toolId, UpdateToolRequest req) =>
            {
                var tool = new AgentTool
                {
                    Id = toolId,
                    Name = req.Name,
                    Description = req.Description,
                    ParametersSchema = req.ParametersSchema,
                    IsEnabled = req.IsEnabled
                };

                var success = await repo.UpdateTool(tool);
                return success ? Results.Ok() : Results.BadRequest(new { error = "Failed to update tool." });
            });

            group.MapDelete("/tools/{toolId:guid}", async (IAgentRepository repo, Guid toolId) =>
            {
                var success = await repo.DeleteTool(toolId);
                return success ? Results.Ok() : Results.NotFound();
            });

            // ── Built-in Tools ───────────────────────────────────────────

            group.MapGet("/builtin-tools", () =>
            {
                var definitions = BuiltinTools.GetDefinitions();
                return Results.Ok(definitions);
            });

            group.MapPost("/{agentId:guid}/seed-builtin-tools", async (IAgentRepository repo, Guid agentId) =>
            {
                var existingTools = await repo.GetAgentTools(agentId);
                var definitions = BuiltinTools.GetDefinitions();
                var added = 0;
                var updated = 0;

                var existingBuiltinTools = (existingTools ?? [])
                    .Where(t => t.Source == AgentToolSource.Builtin)
                    .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

                foreach (var def in definitions)
                {
                    if (existingBuiltinTools.TryGetValue(def.Name, out var existingTool))
                    {
                        if (existingTool.Description == def.Description
                            && existingTool.ParametersSchema == def.ParametersSchema)
                        {
                            continue;
                        }

                        existingTool.Description = def.Description;
                        existingTool.ParametersSchema = def.ParametersSchema;

                        var updateResult = await repo.UpdateTool(existingTool);
                        if (updateResult)
                        {
                            updated++;
                        }

                        continue;
                    }

                    var tool = new AgentTool
                    {
                        AgentDefinitionId = agentId,
                        Name = def.Name,
                        Description = def.Description,
                        ParametersSchema = def.ParametersSchema,
                        IsEnabled = true,
                        Source = AgentToolSource.Builtin
                    };

                    var result = await repo.AddTool(tool);
                    if (result is not null) added++;
                }

                return Results.Ok(new { added, updated });
            });

            // ── MCP Servers ──────────────────────────────────────────────

            group.MapPost("/{agentId:guid}/mcp-servers", async (IAgentRepository repo, Guid agentId, CreateMcpServerRequest req) =>
            {
                var server = new AgentMcpServer
                {
                    AgentDefinitionId = agentId,
                    Name = req.Name,
                    Endpoint = req.Endpoint,
                    ApiKey = req.ApiKey,
                    IsActive = true
                };

                var result = await repo.AddMcpServer(server);
                if (result is null)
                {
                    return Results.BadRequest(new { error = "Failed to add MCP server." });
                }

                result.ApiKey = null;
                return Results.Ok(result);
            });

            group.MapGet("/{agentId:guid}/mcp-servers", async (IAgentRepository repo, Guid agentId) =>
            {
                var servers = await repo.GetAgentMcpServers(agentId);
                if (servers is not null)
                {
                    foreach (var s in servers) s.ApiKey = null;
                }
                return Results.Ok(servers);
            });

            group.MapPut("/mcp-servers/{serverId:guid}", async (IAgentRepository repo, Guid serverId, UpdateMcpServerRequest req) =>
            {
                var server = new AgentMcpServer
                {
                    Id = serverId,
                    Name = req.Name,
                    Endpoint = req.Endpoint,
                    ApiKey = req.ApiKey,
                    IsActive = req.IsActive
                };

                var success = await repo.UpdateMcpServer(server);
                return success ? Results.Ok() : Results.BadRequest(new { error = "Failed to update MCP server." });
            });

            group.MapDelete("/mcp-servers/{serverId:guid}", async (IAgentRepository repo, Guid serverId) =>
            {
                var success = await repo.DeleteMcpServer(serverId);
                return success ? Results.Ok() : Results.NotFound();
            });

            // ── MCP Tool Discovery ───────────────────────────────────────

            group.MapPost("/mcp-servers/{serverId:guid}/discover", async (
                IAgentRepository repo,
                McpClientService mcpClient,
                EncryptionService encryptionService,
                Guid serverId) =>
            {
                var server = await repo.GetMcpServer(serverId);
                if (server is null)
                {
                    return Results.NotFound(new { error = "MCP server not found." });
                }

                // Decrypt API key for the MCP call
                string? apiKey = null;
                if (!string.IsNullOrWhiteSpace(server.ApiKey))
                {
                    try { apiKey = encryptionService.Decrypt(server.ApiKey); }
                    catch { /* ignore */ }
                }

                var discovered = await mcpClient.DiscoverToolsAsync(server.Endpoint, apiKey);
                if (discovered.Count == 0)
                {
                    return Results.Ok(new { discovered = 0, tools = Array.Empty<object>() });
                }

                // Remove old MCP tools for this server, then add fresh ones
                await repo.DeleteToolsByMcpServer(serverId);

                var addedTools = new List<AgentTool>();
                foreach (var mcpTool in discovered)
                {
                    var tool = new AgentTool
                    {
                        AgentDefinitionId = server.AgentDefinitionId,
                        Name = mcpTool.Name,
                        Description = mcpTool.Description ?? mcpTool.Name,
                        ParametersSchema = mcpTool.InputSchema?.ToString(),
                        IsEnabled = true,
                        Source = AgentToolSource.Mcp,
                        McpServerId = serverId
                    };

                    var result = await repo.AddTool(tool);
                    if (result is not null) addedTools.Add(result);
                }

                // Update last discovered timestamp
                await repo.UpdateMcpServer(new AgentMcpServer
                {
                    Id = serverId,
                    Name = server.Name,
                    Endpoint = server.Endpoint,
                    IsActive = server.IsActive,
                    LastDiscoveredAt = DateTimeOffset.UtcNow
                });

                return Results.Ok(new { discovered = addedTools.Count, tools = addedTools });
            });

            // ── Conversations ────────────────────────────────────────────

            group.MapPost("/{agentId:guid}/conversations", async (IAgentRepository repo, Guid agentId, CreateConversationRequest? req) =>
            {
                var result = await repo.CreateConversation(agentId, req?.Title);
                return result is not null ? Results.Ok(result) : Results.BadRequest(new { error = "Failed to create conversation." });
            });

            group.MapGet("/{agentId:guid}/conversations", async (IAgentRepository repo, Guid agentId) =>
            {
                var conversations = await repo.GetUserConversations(agentId);
                return Results.Ok(conversations);
            });

            group.MapGet("/conversations/{conversationId:guid}", async (IAgentRepository repo, Guid conversationId) =>
            {
                var conversation = await repo.GetConversation(conversationId);
                return conversation is not null ? Results.Ok(conversation) : Results.NotFound();
            });

            group.MapDelete("/conversations/{conversationId:guid}", async (IAgentRepository repo, Guid conversationId) =>
            {
                var success = await repo.DeleteConversation(conversationId);
                return success ? Results.Ok() : Results.NotFound();
            });

            group.MapPost("/conversations/{conversationId:guid}/clear-memory", async (IAgentRepository repo, Guid conversationId) =>
            {
                var success = await repo.UpdateConversationSessionState(conversationId, null);
                return success ? Results.Ok() : Results.NotFound();
            });

            group.MapDelete("/{agentId:guid}/conversations", async (IAgentRepository repo, Guid agentId) =>
            {
                var deleted = await repo.DeleteAllConversations(agentId);
                return Results.Ok(new { deleted });
            });

            // ── Messages ─────────────────────────────────────────────────

            group.MapGet("/conversations/{conversationId:guid}/messages", async (IAgentRepository repo, Guid conversationId, int? skip, int? take) =>
            {
                var messages = await repo.GetConversationMessages(conversationId, skip ?? 0, take ?? 50);
                return Results.Ok(messages);
            });

            // ── Chat (invoke agent) ──────────────────────────────────────

            group.MapPost("/conversations/{conversationId:guid}/chat", async (
                IAgentRepository repo,
                AgentRuntimeService runtime,
                HttpContext httpContext,
                Guid conversationId,
                AgentChatRequest req) =>
            {
                // Get user ID from the JWT token
                var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out Guid userId))
                {
                    return Results.Unauthorized();
                }

                var response = await runtime.ChatAsync(userId, conversationId, req);
                return response.Success ? Results.Ok(response) : Results.BadRequest(response);
            });

            // ── Chat Streaming (invoke agent with SSE) ───────────────────

            group.MapPost("/conversations/{conversationId:guid}/chat/stream", async (
                AgentRuntimeService runtime,
                HttpContext httpContext,
                Guid conversationId,
                AgentChatRequest req) =>
            {
                var userIdClaim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out Guid userId))
                {
                    httpContext.Response.StatusCode = 401;
                    return;
                }

                httpContext.Response.ContentType = "text/event-stream";
                httpContext.Response.Headers["Cache-Control"] = "no-cache";
                httpContext.Response.Headers["Connection"] = "keep-alive";

                await foreach (var chunk in runtime.ChatStreamAsync(userId, conversationId, req))
                {
                    await WriteSseEventAsync(httpContext, chunk);
                    await httpContext.Response.Body.FlushAsync();
                }

                await WriteSseEventAsync(httpContext, "[DONE]");
                await httpContext.Response.Body.FlushAsync();
            });

            // ── Publishing ───────────────────────────────────────────────

            group.MapPost("/{agentId:guid}/publish", async (IAgentRepository repo, Guid agentId) =>
            {
                var success = await repo.PublishAgent(agentId, true);
                return success ? Results.Ok() : Results.BadRequest(new { error = "Failed to publish agent." });
            });

            group.MapPost("/{agentId:guid}/unpublish", async (IAgentRepository repo, Guid agentId) =>
            {
                var success = await repo.PublishAgent(agentId, false);
                return success ? Results.Ok() : Results.BadRequest(new { error = "Failed to unpublish agent." });
            });

            group.MapGet("/published/search", async (IAgentRepository repo, string query, int? maxResults) =>
            {
                var agents = await repo.SearchPublishedAgents(query, maxResults ?? 20);
                if (agents is not null)
                {
                    foreach (var a in agents) StripSecrets(a);
                }
                return Results.Ok(agents);
            }).AllowAnonymous();

            group.MapGet("/published/{agentId:guid}", async (IAgentRepository repo, Guid agentId) =>
            {
                var agent = await repo.GetPublishedAgent(agentId);
                if (agent is null) return Results.NotFound();
                StripSecrets(agent);
                return Results.Ok(agent);
            }).AllowAnonymous();

            // ── Agent Friends ────────────────────────────────────────────

            group.MapPost("/friends/{agentId:guid}", async (IAgentRepository repo, Guid agentId) =>
            {
                var success = await repo.AddAgentFriend(agentId);
                return success ? Results.Ok() : Results.BadRequest(new { error = "Failed to add agent as friend." });
            });

            group.MapDelete("/friends/{agentId:guid}", async (IAgentRepository repo, Guid agentId) =>
            {
                var success = await repo.RemoveAgentFriend(agentId);
                return success ? Results.Ok() : Results.NotFound();
            });

            group.MapGet("/friends", async (IAgentRepository repo) =>
            {
                var friends = await repo.GetUserAgentFriends();
                return Results.Ok(friends);
            });

            group.MapGet("/friends/{agentId:guid}/check", async (IAgentRepository repo, Guid agentId) =>
            {
                var isFriend = await repo.IsAgentFriend(agentId);
                return Results.Ok(new { isFriend });
            });

            // ── Channel Agents ───────────────────────────────────────────

            group.MapPost("/channel/{channelId:guid}/agents/{agentId:guid}", async (IAgentRepository repo, Guid channelId, Guid agentId) =>
            {
                var success = await repo.AddAgentToChannel(channelId, agentId);
                return success ? Results.Ok() : Results.BadRequest(new { error = "Failed to add agent to channel." });
            });

            group.MapDelete("/channel/{channelId:guid}/agents/{agentId:guid}", async (IAgentRepository repo, Guid channelId, Guid agentId) =>
            {
                var success = await repo.RemoveAgentFromChannel(channelId, agentId);
                return success ? Results.Ok() : Results.NotFound();
            });

            group.MapGet("/channel/{channelId:guid}/agents", async (IAgentRepository repo, Guid channelId) =>
            {
                var agents = await repo.GetChannelAgentDefinitions(channelId);
                if (agents is not null)
                {
                    foreach (var a in agents) StripSecrets(a);
                }
                return Results.Ok(agents);
            });
        }

        private static void StripSecrets(AgentDefinition agent)
        {
            agent.ApiKey = null;
            agent.EncryptionPrivateKey = null;
        }

        private static Task WriteSseEventAsync(HttpContext httpContext, string data)
        {
            var builder = new StringBuilder();
            var normalized = (data ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = normalized.Split('\n');

            foreach (var line in lines)
            {
                builder.Append("data: ");
                builder.Append(line);
                builder.Append('\n');
            }

            builder.Append('\n');
            return httpContext.Response.WriteAsync(builder.ToString());
        }

        private static string ResolveSkillDirectoryName(string? requestedName, string path)
        {
            if (!string.IsNullOrWhiteSpace(requestedName))
            {
                return requestedName.Trim();
            }

            var normalizedPath = path.Replace('\\', '/').TrimEnd('/');
            var lastSegment = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (string.IsNullOrWhiteSpace(lastSegment))
            {
                throw new ArgumentException("A skill name or path is required.", nameof(path));
            }

            return lastSegment;
        }
    }

    // ── Request DTOs ─────────────────────────────────────────────────────

    [ExcludeFromCodeCoverage]
    public record CreateAgentRequest(
        string Name,
        string? Description,
        string? Instructions,
        LlmProvider Provider,
        string Model,
        string? ApiKey,
        string? Endpoint,
        bool IsPublished,
        AgentAddPermission AddPermission,
        float? Temperature,
        int? MaxTokens,
        string? SkillsInstructionPrompt);

    [ExcludeFromCodeCoverage]
    public record UpdateAgentRequest(
        string Name,
        string? Description,
        string? Instructions,
        LlmProvider Provider,
        string Model,
        string? ApiKey,
        string? Endpoint,
        bool IsActive,
        bool IsPublished,
        AgentAddPermission AddPermission,
        float? Temperature,
        int? MaxTokens,
        string? SkillsInstructionPrompt);

    [ExcludeFromCodeCoverage]
    public record CreateSkillDirectoryRequest(
        string Name,
        string Path,
        bool IsEnabled = true);

    [ExcludeFromCodeCoverage]
    public record UpdateSkillDirectoryRequest(
        string Name,
        string Path,
        bool IsEnabled = true);

    [ExcludeFromCodeCoverage]
    public record CreateManagedSkillRequest(
        string Name,
        string Content,
        string? FileName = null,
        bool IsEnabled = true);

    [ExcludeFromCodeCoverage]
    public record CreateKnowledgeRequest(
        string Title,
        string Content,
        bool IsEnabled = true);

    [ExcludeFromCodeCoverage]
    public record UpdateKnowledgeRequest(
        string Title,
        string Content,
        bool IsEnabled = true);

    [ExcludeFromCodeCoverage]
    public record CreateToolRequest(
        string Name,
        string Description,
        string? ParametersSchema,
        bool IsEnabled = true,
        AgentToolSource Source = AgentToolSource.Builtin,
        Guid? McpServerId = null);

    [ExcludeFromCodeCoverage]
    public record UpdateToolRequest(
        string Name,
        string Description,
        string? ParametersSchema,
        bool IsEnabled = true);

    [ExcludeFromCodeCoverage]
    public record CreateMcpServerRequest(
        string Name,
        string Endpoint,
        string? ApiKey);

    [ExcludeFromCodeCoverage]
    public record UpdateMcpServerRequest(
        string Name,
        string Endpoint,
        string? ApiKey,
        bool IsActive = true);

    [ExcludeFromCodeCoverage]
    public record CreateConversationRequest(string? Title);
}
