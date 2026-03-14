using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;

namespace Egroo.Server.Services
{
    /// <summary>
    /// Client service for connecting to MCP (Model Context Protocol) servers.
    /// Supports tool discovery via tools/list and tool invocation via tools/call
    /// using the MCP Streamable HTTP transport (JSON-RPC over HTTP POST).
    /// </summary>
    public class McpClientService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<McpClientService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public McpClientService(IHttpClientFactory httpClientFactory, ILogger<McpClientService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Discover available tools from an MCP server endpoint.
        /// Sends a JSON-RPC "tools/list" request to the server.
        /// </summary>
        public async Task<List<McpToolInfo>> DiscoverToolsAsync(string endpoint, string? apiKey = null)
        {
            var tools = new List<McpToolInfo>();

            try
            {
                var client = _httpClientFactory.CreateClient("McpClient");
                ConfigureAuth(client, apiKey);

                var request = new JsonRpcRequest
                {
                    Method = "tools/list",
                    Id = Guid.NewGuid().ToString()
                };

                var json = JsonSerializer.Serialize(request, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(endpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("MCP tools/list failed with status {Status} for {Endpoint}",
                        response.StatusCode, endpoint);
                    return tools;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var rpcResponse = JsonSerializer.Deserialize<JsonRpcResponse<McpToolsListResult>>(responseBody, JsonOptions);

                if (rpcResponse?.Result?.Tools is not null)
                {
                    tools.AddRange(rpcResponse.Result.Tools);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to discover tools from MCP server at {Endpoint}", endpoint);
            }

            return tools;
        }

        /// <summary>
        /// Call a tool on an MCP server. Sends a JSON-RPC "tools/call" request.
        /// Returns the text content of the tool result.
        /// </summary>
        public async Task<string> CallToolAsync(string endpoint, string? apiKey, string toolName, JsonElement? arguments)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("McpClient");
                ConfigureAuth(client, apiKey);
                var request = new JsonRpcRequest
                {
                    Method = "tools/call",
                    Params = BuildCallParameters(toolName, arguments),
                    Id = Guid.NewGuid().ToString()
                };

                var json = JsonSerializer.Serialize(request, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(endpoint, content);
                if (!response.IsSuccessStatusCode)
                {
                    return FormatServerError(response.StatusCode);
                }

                var rpcResponse = await DeserializeCallResponseAsync(response);
                return FormatCallResponse(rpcResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call tool '{Tool}' on MCP server at {Endpoint}", toolName, endpoint);
                return $"[MCP Error] {ex.Message}";
            }
        }

        private static Dictionary<string, object?> BuildCallParameters(string toolName, JsonElement? arguments)
        {
            var rpcParams = new Dictionary<string, object?>
            {
                ["name"] = toolName,
            };

            if (arguments is not null && arguments.Value.ValueKind != JsonValueKind.Undefined)
            {
                rpcParams["arguments"] = arguments;
            }

            return rpcParams;
        }

        private static string FormatServerError(System.Net.HttpStatusCode statusCode)
        {
            return $"[MCP Error] Server returned {statusCode}";
        }

        private static async Task<JsonRpcResponse<McpToolCallResult>?> DeserializeCallResponseAsync(HttpResponseMessage response)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonRpcResponse<McpToolCallResult>>(responseBody, JsonOptions);
        }

        private static string FormatCallResponse(JsonRpcResponse<McpToolCallResult>? rpcResponse)
        {
            if (rpcResponse?.Error is not null)
            {
                return $"[MCP Error] {rpcResponse.Error.Message}";
            }

            if (rpcResponse?.Result?.Content is null)
            {
                return string.Empty;
            }

            return JoinTextContent(rpcResponse.Result.Content);
        }

        private static string JoinTextContent(IEnumerable<McpToolContent> content)
        {
            var sb = new StringBuilder();
            foreach (var item in content)
            {
                if (item.Type == "text" && item.Text is not null)
                {
                    sb.AppendLine(item.Text);
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Create AITool wrappers for MCP tools so they can be invoked by the AI agent.
        /// Each MCP tool becomes a callable function that proxies to the MCP server.
        /// </summary>
        public IList<AITool> CreateMcpAITools(
            IEnumerable<McpToolProxy> mcpTools)
        {
            var tools = new List<AITool>();

            foreach (var mcp in mcpTools)
            {
                // Capture in closure
                var endpoint = mcp.Endpoint;
                var apiKey = mcp.ApiKey;
                var toolName = mcp.ToolName;

                var tool = AIFunctionFactory.Create(
                    async (string input) =>
                    {
                        // Parse the input as JSON arguments
                        JsonElement? args = null;
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(input))
                            {
                                args = JsonSerializer.Deserialize<JsonElement>(input);
                            }
                        }
                        catch
                        {
                            // If input isn't valid JSON, wrap it
                            args = JsonSerializer.Deserialize<JsonElement>(
                                JsonSerializer.Serialize(new { input }));
                        }

                        return await CallToolAsync(endpoint, apiKey, toolName, args);
                    },
                    toolName,
                    mcp.Description ?? $"MCP tool: {toolName}");

                tools.Add(tool);
            }

            return tools;
        }

        private static void ConfigureAuth(HttpClient client, string? apiKey)
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }
        }
    }

    // ── MCP JSON-RPC Protocol Types ─────────────────────────────────────

    [ExcludeFromCodeCoverage]
    public class JsonRpcRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public object? Params { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class JsonRpcResponse<T>
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        [JsonPropertyName("result")]
        public T? Result { get; set; }

        [JsonPropertyName("error")]
        public JsonRpcError? Error { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class JsonRpcError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    [ExcludeFromCodeCoverage]
    public class McpToolsListResult
    {
        [JsonPropertyName("tools")]
        public List<McpToolInfo>? Tools { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class McpToolInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("inputSchema")]
        public JsonElement? InputSchema { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class McpToolCallResult
    {
        [JsonPropertyName("content")]
        public List<McpToolContent>? Content { get; set; }

        [JsonPropertyName("isError")]
        public bool? IsError { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class McpToolContent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    /// <summary>
    /// Represents an MCP tool ready to be wired as an AITool proxy.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class McpToolProxy
    {
        public string Endpoint { get; set; } = string.Empty;
        public string? ApiKey { get; set; }
        public string ToolName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
