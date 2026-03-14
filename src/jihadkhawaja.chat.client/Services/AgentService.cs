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
                def.IsPublished,
                def.AddPermission,
                def.Temperature,
                def.MaxTokens,
                def.SkillsInstructionPrompt
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
                def.IsPublished,
                def.AddPermission,
                def.Temperature,
                def.MaxTokens,
                def.SkillsInstructionPrompt
            };

            var response = await HttpClient.PutAsJsonAsync($"{BasePath}/{def.Id}", payload);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteAgent(Guid agentId)
        {
            var response = await HttpClient.DeleteAsync($"{BasePath}/{agentId}");
            return response.IsSuccessStatusCode;
        }

        // ── Agent Skills ─────────────────────────────────────────────

        public async Task<AgentSkillDirectory?> AddSkillDirectory(Guid agentId, AgentSkillDirectory skillDirectory)
        {
            var payload = new { skillDirectory.Name, skillDirectory.Path, skillDirectory.IsEnabled };
            var response = await HttpClient.PostAsJsonAsync($"{BasePath}/{agentId}/skills", payload);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AgentSkillDirectory>(JsonOptions);
        }

        public async Task<AgentSkillDirectory?> AddManagedSkill(Guid agentId, string name, string content, string? fileName = null, bool isEnabled = true)
        {
            var payload = new { Name = name, Content = content, FileName = fileName, IsEnabled = isEnabled };
            var response = await HttpClient.PostAsJsonAsync($"{BasePath}/{agentId}/skills/managed", payload);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AgentSkillDirectory>(JsonOptions);
        }

        public async Task<AgentSkillDirectory[]?> GetSkillDirectories(Guid agentId)
        {
            return await HttpClient.GetFromJsonAsync<AgentSkillDirectory[]>($"{BasePath}/{agentId}/skills", JsonOptions);
        }

        public async Task<bool> UpdateSkillDirectory(AgentSkillDirectory skillDirectory)
        {
            var payload = new { skillDirectory.Name, skillDirectory.Path, skillDirectory.IsEnabled };
            var response = await HttpClient.PutAsJsonAsync($"{BasePath}/skills/{skillDirectory.Id}", payload);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteSkillDirectory(Guid skillDirectoryId)
        {
            var response = await HttpClient.DeleteAsync($"{BasePath}/skills/{skillDirectoryId}");
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
            var dataLines = new List<string>();
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                if (string.IsNullOrEmpty(line))
                {
                    if (dataLines.Count == 0)
                    {
                        continue;
                    }

                    var data = string.Join("\n", dataLines);
                    dataLines.Clear();

                    if (data == "[DONE]")
                    {
                        yield break;
                    }

                    yield return data;
                    continue;
                }

                if (!line.StartsWith("data:", StringComparison.Ordinal))
                {
                    continue;
                }

                string dataLine = line.Length >= 6 && line[5] == ' '
                    ? line[6..]
                    : line[5..];
                dataLines.Add(dataLine);
            }

            if (dataLines.Count > 0)
            {
                var data = string.Join("\n", dataLines);
                if (data != "[DONE]")
                {
                    yield return data;
                }
            }
        }

        // ── Publishing ───────────────────────────────────────────────

        public async Task<bool> PublishAgent(Guid agentId)
        {
            var response = await HttpClient.PostAsync($"{BasePath}/{agentId}/publish", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UnpublishAgent(Guid agentId)
        {
            var response = await HttpClient.PostAsync($"{BasePath}/{agentId}/unpublish", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<AgentDefinition[]?> SearchPublishedAgents(string? query = null)
        {
            var url = $"{BasePath}/published/search";
            if (!string.IsNullOrWhiteSpace(query))
                url += $"?query={Uri.EscapeDataString(query)}";
            return await HttpClient.GetFromJsonAsync<AgentDefinition[]>(url, JsonOptions);
        }

        public async Task<AgentDefinition?> GetPublishedAgent(Guid agentId)
        {
            return await HttpClient.GetFromJsonAsync<AgentDefinition>($"{BasePath}/published/{agentId}", JsonOptions);
        }

        // ── Agent Friends ────────────────────────────────────────────

        public async Task<bool> AddAgentFriend(Guid agentId)
        {
            var response = await HttpClient.PostAsync($"{BasePath}/friends/{agentId}", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> RemoveAgentFriend(Guid agentId)
        {
            var response = await HttpClient.DeleteAsync($"{BasePath}/friends/{agentId}");
            return response.IsSuccessStatusCode;
        }

        public async Task<UserAgentFriend[]?> GetAgentFriends()
        {
            return await HttpClient.GetFromJsonAsync<UserAgentFriend[]>($"{BasePath}/friends", JsonOptions);
        }

        public async Task<bool> IsAgentFriend(Guid agentId)
        {
            try
            {
                var result = await HttpClient.GetFromJsonAsync<bool>($"{BasePath}/friends/{agentId}/check", JsonOptions);
                return result;
            }
            catch
            {
                return false;
            }
        }

        // ── Channel Agents ───────────────────────────────────────────

        public async Task<bool> AddAgentToChannel(Guid channelId, Guid agentId)
        {
            var response = await HttpClient.PostAsync($"{BasePath}/channel/{channelId}/agents/{agentId}", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> RemoveAgentFromChannel(Guid channelId, Guid agentId)
        {
            var response = await HttpClient.DeleteAsync($"{BasePath}/channel/{channelId}/agents/{agentId}");
            return response.IsSuccessStatusCode;
        }

        public async Task<AgentDefinition[]?> GetChannelAgents(Guid channelId)
        {
            return await HttpClient.GetFromJsonAsync<AgentDefinition[]>($"{BasePath}/channel/{channelId}/agents", JsonOptions);
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
        public int Updated { get; set; }
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
