using Egroo.Server.Services;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace Egroo.Server.Test;

[TestClass]
public class McpClientServiceTest
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Fake HttpMessageHandler that returns a canned response.
    /// </summary>
    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public FakeHttpHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>
    /// IHttpClientFactory that returns an HttpClient backed by a provided handler.
    /// </summary>
    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public FakeHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) => new(_handler) { BaseAddress = new Uri("http://localhost") };
    }

    private static McpClientService CreateService(FakeHttpHandler handler)
    {
        var factory = new FakeHttpClientFactory(handler);
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<McpClientService>();
        return new McpClientService(factory, logger);
    }

    // ── DiscoverToolsAsync ──────────────────────────────────────────────────

    [TestMethod]
    public async Task DiscoverToolsAsync_SuccessfulResponse_ReturnsTools()
    {
        var json = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            result = new
            {
                tools = new[]
                {
                    new { name = "read_file", description = "Reads a file" },
                    new { name = "write_file", description = "Writes a file" }
                }
            },
            id = "1"
        });

        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var tools = await service.DiscoverToolsAsync("http://mcp.example.com/rpc");

        Assert.AreEqual(2, tools.Count);
        Assert.AreEqual("read_file", tools[0].Name);
        Assert.AreEqual("write_file", tools[1].Name);
    }

    [TestMethod]
    public async Task DiscoverToolsAsync_WithApiKey_SetsAuthorizationHeader()
    {
        var json = JsonSerializer.Serialize(new { jsonrpc = "2.0", result = new { tools = Array.Empty<object>() }, id = "1" });
        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        await service.DiscoverToolsAsync("http://mcp.example.com/rpc", "secret-key-123");

        Assert.IsNotNull(handler.LastRequest);
        Assert.AreEqual("Bearer", handler.LastRequest!.Headers.Authorization?.Scheme);
        Assert.AreEqual("secret-key-123", handler.LastRequest.Headers.Authorization?.Parameter);
    }

    [TestMethod]
    public async Task DiscoverToolsAsync_NonSuccessStatus_ReturnsEmptyList()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError, "{}");
        var service = CreateService(handler);

        var tools = await service.DiscoverToolsAsync("http://mcp.example.com/rpc");

        Assert.AreEqual(0, tools.Count);
    }

    [TestMethod]
    public async Task DiscoverToolsAsync_NullResultTools_ReturnsEmptyList()
    {
        var json = JsonSerializer.Serialize(new { jsonrpc = "2.0", result = new { tools = (object?)null }, id = "1" });
        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var tools = await service.DiscoverToolsAsync("http://mcp.example.com/rpc");

        Assert.AreEqual(0, tools.Count);
    }

    [TestMethod]
    public async Task DiscoverToolsAsync_EmptyResponse_ReturnsEmptyList()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);

        var tools = await service.DiscoverToolsAsync("http://mcp.example.com/rpc");

        Assert.AreEqual(0, tools.Count);
    }

    [TestMethod]
    public async Task DiscoverToolsAsync_NoApiKey_NoAuthHeader()
    {
        var json = JsonSerializer.Serialize(new { jsonrpc = "2.0", result = new { tools = Array.Empty<object>() }, id = "1" });
        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        await service.DiscoverToolsAsync("http://mcp.example.com/rpc");

        Assert.IsNotNull(handler.LastRequest);
        Assert.IsNull(handler.LastRequest!.Headers.Authorization);
    }

    // ── CallToolAsync ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task CallToolAsync_SuccessfulResponse_ReturnsTextContent()
    {
        var json = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            result = new
            {
                content = new[]
                {
                    new { type = "text", text = "Hello from tool" }
                }
            },
            id = "1"
        });

        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var result = await service.CallToolAsync("http://mcp.example.com/rpc", null, "greet", null);

        Assert.AreEqual("Hello from tool", result);
    }

    [TestMethod]
    public async Task CallToolAsync_MultipleTextContent_JoinsWithNewline()
    {
        var json = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            result = new
            {
                content = new[]
                {
                    new { type = "text", text = "Line 1" },
                    new { type = "text", text = "Line 2" }
                }
            },
            id = "1"
        });

        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var result = await service.CallToolAsync("http://mcp.example.com/rpc", null, "multi", null);

        Assert.IsTrue(result.Contains("Line 1"));
        Assert.IsTrue(result.Contains("Line 2"));
    }

    [TestMethod]
    public async Task CallToolAsync_NonSuccessStatus_ReturnsErrorMessage()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.ServiceUnavailable, "");
        var service = CreateService(handler);

        var result = await service.CallToolAsync("http://mcp.example.com/rpc", null, "fail", null);

        Assert.IsTrue(result.Contains("[MCP Error]"));
        Assert.IsTrue(result.Contains("ServiceUnavailable"));
    }

    [TestMethod]
    public async Task CallToolAsync_RpcError_ReturnsErrorMessage()
    {
        var json = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            error = new { code = -32601, message = "Method not found" },
            id = "1"
        });

        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var result = await service.CallToolAsync("http://mcp.example.com/rpc", null, "missing", null);

        Assert.IsTrue(result.Contains("[MCP Error]"));
        Assert.IsTrue(result.Contains("Method not found"));
    }

    [TestMethod]
    public async Task CallToolAsync_NullResultContent_ReturnsEmpty()
    {
        var json = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            result = new { content = (object?)null },
            id = "1"
        });

        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var result = await service.CallToolAsync("http://mcp.example.com/rpc", null, "empty", null);

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public async Task CallToolAsync_WithJsonArguments_IncludesInRequest()
    {
        var json = JsonSerializer.Serialize(new { jsonrpc = "2.0", result = new { content = Array.Empty<object>() }, id = "1" });
        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var args = JsonSerializer.Deserialize<JsonElement>("{\"path\":\"/tmp/file.txt\"}");

        await service.CallToolAsync("http://mcp.example.com/rpc", "key", "read_file", args);

        Assert.IsNotNull(handler.LastRequest);
        Assert.AreEqual("Bearer", handler.LastRequest!.Headers.Authorization?.Scheme);
    }

    [TestMethod]
    public async Task CallToolAsync_NonTextContent_IsFiltered()
    {
        var json = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            result = new
            {
                content = new[]
                {
                    new { type = "image", text = "not-shown" },
                    new { type = "text", text = "shown" }
                }
            },
            id = "1"
        });

        var handler = new FakeHttpHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var result = await service.CallToolAsync("http://mcp.example.com/rpc", null, "mixed", null);

        Assert.AreEqual("shown", result);
    }

    // ── CreateMcpAITools ────────────────────────────────────────────────────

    [TestMethod]
    public void CreateMcpAITools_EmptyList_ReturnsEmpty()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);

        var tools = service.CreateMcpAITools(Array.Empty<McpToolProxy>());

        Assert.AreEqual(0, tools.Count);
    }

    [TestMethod]
    public void CreateMcpAITools_SingleTool_CreatesAITool()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);

        var proxies = new[]
        {
            new McpToolProxy
            {
                Endpoint = "http://mcp.example.com/rpc",
                ToolName = "search",
                Description = "Search the web"
            }
        };

        var tools = service.CreateMcpAITools(proxies);

        Assert.AreEqual(1, tools.Count);
    }

    [TestMethod]
    public void CreateMcpAITools_MultipleTools_CreatesAll()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);

        var proxies = new[]
        {
            new McpToolProxy { Endpoint = "http://a.com", ToolName = "tool1" },
            new McpToolProxy { Endpoint = "http://b.com", ToolName = "tool2", Description = "desc2" },
            new McpToolProxy { Endpoint = "http://c.com", ToolName = "tool3", ApiKey = "key3" },
        };

        var tools = service.CreateMcpAITools(proxies);

        Assert.AreEqual(3, tools.Count);
    }

    [TestMethod]
    public void CreateMcpAITools_NullDescription_UsesFallback()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);

        var proxies = new[]
        {
            new McpToolProxy { Endpoint = "http://a.com", ToolName = "my_tool", Description = null }
        };

        var tools = service.CreateMcpAITools(proxies);

        Assert.AreEqual(1, tools.Count);
    }
}
