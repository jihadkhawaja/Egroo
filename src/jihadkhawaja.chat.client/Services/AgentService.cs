using jihadkhawaja.chat.shared.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;

namespace jihadkhawaja.chat.client.Services
{
    /// <summary>
    /// Client-side HTTP service for Agent API endpoints.
    /// </summary>
    public class AgentService
    {
        private const string BasePath = "api/v1/Agent";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public HttpClient HttpClient { get; }

        public AgentService(HttpClient http)
        {
            HttpClient = http;
        }

        // ── Agent CRUD ───────────────────────────────────────────────

        public async Task<AgentDefinition?> CreateAgent(AgentDefinition def)
        {
            var payload = new
            {
                def.Name,
                def.Description,
                def.Instructions,
                def.Provider,
                def.Model,
                def.ApiKey,
                def.Endpoint,
                def.Temperature,
                def.MaxTokens
            };

            var response = await HttpClient.PostAsJsonAsync(BasePath, payload);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AgentDefinition>(JsonOptions);
        }

        public async Task<AgentDefinition[]?> GetAgents()
        {
            return await HttpClient.GetFromJsonAsync<AgentDefinition[]>(BasePath, JsonOptions);
        }

        public async Task<AgentDefinition?> GetAgent(Guid agentId)
        {
            return await HttpClient.GetFromJsonAsync<AgentDefinition>($"{BasePath}/{agentId}", JsonOptions);
        }

        public async Task<bool> UpdateAgent(AgentDefinition def)
        {
            var payload = new
            {
                def.Name,
                def.Description,
                def.Instructions,
                def.Provider,
                def.Model,
                def.ApiKey,
                def.Endpoint,
                def.IsActive,
                def.Temperature,
                def.MaxTokens
            };

            var response = await HttpClient.PutAsJsonAsync($"{BasePath}/{def.Id}", payload);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteAgent(Guid agentId)
        {
            var response = await HttpClient.DeleteAsync($"{BasePath}/{agentId}");
            return response.IsSuccessStatusCode;
        }

        // ── Knowledge ────────────────────────────────────────────────

        public async Task<AgentKnowledge?> AddKnowledge(Guid agentId, AgentKnowledge knowledge)
        {
            var payload = new { knowledge.Title, knowledge.Content, knowledge.IsEnabled };
            var response = await HttpClient.PostAsJsonAsync($"{BasePath}/{agentId}/knowledge", payload);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AgentKnowledge>(JsonOptions);
        }

        public async Task<AgentKnowledge[]?> GetKnowledge(Guid agentId)
        {
            return await HttpClient.GetFromJsonAsync<AgentKnowledge[]>($"{BasePath}/{agentId}/knowledge", JsonOptions);
        }

        public async Task<bool> UpdateKnowledge(AgentKnowledge knowledge)
        {
            var payload = new { knowledge.Title, knowledge.Content, knowledge.IsEnabled };
            var response = await HttpClient.PutAsJsonAsync($"{BasePath}/knowledge/{knowledge.Id}", payload);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteKnowledge(Guid knowledgeId)
        {
            var response = await HttpClient.DeleteAsync($"{BasePath}/knowledge/{knowledgeId}");
            return response.IsSuccessStatusCode;
        }

        // ── Tools ────────────────────────────────────────────────────

        public async Task<AgentTool?> AddTool(Guid agentId, AgentTool tool)
        {
            var payload = new
            {
                tool.Name,
                tool.Description,
                tool.ParametersSchema,
                tool.IsEnabled,
                tool.Source,
                tool.McpServerId
            };
            var response = await HttpClient.PostAsJsonAsync($"{BasePath}/{agentId}/tools", payload);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AgentTool>(JsonOptions);
        }

        public async Task<AgentTool[]?> GetTools(Guid agentId)
        {
            return await HttpClient.GetFromJsonAsync<AgentTool[]>($"{BasePath}/{agentId}/tools", JsonOptions);
        }

        public async Task<bool> UpdateTool(AgentTool tool)
        {
            var payload = new { tool.Name, tool.Description, tool.ParametersSchema, tool.IsEnabled };
            var response = await HttpClient.PutAsJsonAsync($"{BasePath}/tools/{tool.Id}", payload);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteTool(Guid toolId)
        {
            var response = await HttpClient.DeleteAsync($"{BasePath}/tools/{toolId}");
            return response.IsSuccessStatusCode;
        }

        // ── Built-in Tools ───────────────────────────────────────────

        public async Task<BuiltinToolDefinition[]?> GetBuiltinTools()
        {
            return await HttpClient.GetFromJsonAsync<BuiltinToolDefinition[]>($"{BasePath}/builtin-tools", JsonOptions);
        }

        public async Task<SeedResult?> SeedBuiltinTools(Guid agentId)
        {
            var response = await HttpClient.PostAsync($"{BasePath}/{agentId}/seed-builtin-tools", null);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<SeedResult>(JsonOptions);
        }

        // ── MCP Servers ──────────────────────────────────────────────

        public async Task<AgentMcpServer?> AddMcpServer(Guid agentId, string name, string endpoint, string? apiKey)
        {
            var payload = new { Name = name, Endpoint = endpoint, ApiKey = apiKey };
            var response = await HttpClient.PostAsJsonAsync($"{BasePath}/{agentId}/mcp-servers", payload);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AgentMcpServer>(JsonOptions);
        }

        public async Task<AgentMcpServer[]?> GetMcpServers(Guid agentId)
        {
            return await HttpClient.GetFromJsonAsync<AgentMcpServer[]>($"{BasePath}/{agentId}/mcp-servers", JsonOptions);
        }

        public async Task<bool> UpdateMcpServer(AgentMcpServer server, string? apiKey = null)
        {
            var payload = new { server.Name, server.Endpoint, ApiKey = apiKey, server.IsActive };
            var response = await HttpClient.PutAsJsonAsync($"{BasePath}/mcp-servers/{server.Id}", payload);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteMcpServer(Guid serverId)
        {
            var response = await HttpClient.DeleteAsync($"{BasePath}/mcp-servers/{serverId}");
            return response.IsSuccessStatusCode;
        }

        public async Task<DiscoverResult?> DiscoverMcpTools(Guid serverId)
        {
            var response = await HttpClient.PostAsync($"{BasePath}/mcp-servers/{serverId}/discover", null);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<DiscoverResult>(JsonOptions);
        }

        // ── Conversations ────────────────────────────────────────────

        public async Task<AgentConversation?> CreateConversation(Guid agentId, string? title = null)
        {
            var payload = new { Title = title };
            var response = await HttpClient.PostAsJsonAsync($"{BasePath}/{agentId}/conversations", payload);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AgentConversation>(JsonOptions);
        }

        public async Task<AgentConversation[]?> GetConversations(Guid agentId)
        {
            return await HttpClient.GetFromJsonAsync<AgentConversation[]>($"{BasePath}/{agentId}/conversations", JsonOptions);
        }

        public async Task<bool> DeleteConversation(Guid conversationId)
        {
            var response = await HttpClient.DeleteAsync($"{BasePath}/conversations/{conversationId}");
            return response.IsSuccessStatusCode;
        }

        // ── Messages ─────────────────────────────────────────────────

        public async Task<AgentConversationMessage[]?> GetMessages(Guid conversationId, int skip = 0, int take = 50)
        {
            return await HttpClient.GetFromJsonAsync<AgentConversationMessage[]>(
                $"{BasePath}/conversations/{conversationId}/messages?skip={skip}&take={take}", JsonOptions);
        }

        // ── Chat (non-streaming) ─────────────────────────────────────

        public async Task<AgentChatResult?> Chat(Guid conversationId, string message)
        {
            var payload = new { Message = message };
            var response = await HttpClient.PostAsJsonAsync($"{BasePath}/conversations/{conversationId}/chat", payload);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AgentChatResult>(JsonOptions);
        }

        // ── Chat Streaming (SSE) ─────────────────────────────────────

        public async IAsyncEnumerable<string> ChatStream(Guid conversationId, string message)
        {
            var payload = new { Message = message };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post,
                $"{BasePath}/conversations/{conversationId}/chat/stream")
            {
                Content = content
            };

            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                yield return "[ERROR] Failed to connect to agent.";
                yield break;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                if (string.IsNullOrEmpty(line)) continue;
                if (!line.StartsWith("data: ")) continue;

                var data = line["data: ".Length..];
                if (data == "[DONE]") yield break;

                yield return data;
            }
        }
    }

    /// <summary>
    /// Result model for non-streaming agent chat.
    /// </summary>
    public class AgentChatResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid? MessageId { get; set; }
        public Guid? ConversationId { get; set; }
    }

    /// <summary>
    /// Built-in tool definition returned by the server.
    /// </summary>
    public class BuiltinToolDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ParametersSchema { get; set; }
    }

    /// <summary>
    /// Result from seeding built-in tools.
    /// </summary>
    public class SeedResult
    {
        public int Added { get; set; }
    }

    /// <summary>
    /// Result from MCP tool discovery.
    /// </summary>
    public class DiscoverResult
    {
        public int Discovered { get; set; }
        public object[]? Tools { get; set; }
    }
}
