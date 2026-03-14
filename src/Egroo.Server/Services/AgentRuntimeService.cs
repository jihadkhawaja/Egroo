using Egroo.Server.Database;
using Egroo.Server.Security;
using jihadkhawaja.chat.shared.Models;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Egroo.Server.Services
{
    /// <summary>
    /// Service that creates and runs AI agents using Microsoft Agent Framework.
    /// Builds AIAgent instances on-the-fly from user-defined AgentDefinition configurations.
    /// </summary>
    public class AgentRuntimeService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AgentSkillsService _agentSkillsService;
        private readonly EncryptionService _encryptionService;
        private readonly McpClientService _mcpClientService;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AgentRuntimeService> _logger;

        public AgentRuntimeService(
            IServiceScopeFactory scopeFactory,
            AgentSkillsService agentSkillsService,
            EncryptionService encryptionService,
            McpClientService mcpClientService,
            ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider,
            ILogger<AgentRuntimeService> logger)
        {
            _scopeFactory = scopeFactory;
            _agentSkillsService = agentSkillsService;
            _encryptionService = encryptionService;
            _mcpClientService = mcpClientService;
            _loggerFactory = loggerFactory;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Send a user message to an agent and get the response.
        /// Manages a full conversation turn: stores user message, runs agent, stores response.
        /// </summary>
        public async Task<AgentChatResponse> ChatAsync(Guid userId, Guid conversationId, string userMessage)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            // Load conversation
            var conversation = await dbContext.AgentConversations
                .FirstOrDefaultAsync(x => x.Id == conversationId && x.UserId == userId && x.DateDeleted == null);

            if (conversation is null)
            {
                return AgentChatResponse.Error("Conversation not found.");
            }

            // Load agent definition
            var agentDef = await dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == conversation.AgentDefinitionId && x.UserId == userId && x.DateDeleted == null);

            if (agentDef is null)
            {
                return AgentChatResponse.Error("Agent not found.");
            }

            if (!agentDef.IsActive)
            {
                return AgentChatResponse.Error("Agent is not active.");
            }

            // Decrypt API key
            string? apiKey = null;
            if (!string.IsNullOrWhiteSpace(agentDef.ApiKey))
            {
                try
                {
                    apiKey = _encryptionService.Decrypt(agentDef.ApiKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to decrypt API key for agent {AgentId}", agentDef.Id);
                    return AgentChatResponse.Error("Failed to decrypt API key.");
                }
            }

            // Build system instructions with knowledge
            string instructions = await BuildInstructions(dbContext, agentDef);

            // Build function tools from agent tool definitions
            var tools = await BuildTools(dbContext, agentDef.Id, userId);

            var skillDirectories = await BuildSkillDirectories(dbContext, agentDef.Id);

            // Create the AIAgent via Microsoft Agent Framework
            AIAgent agent;
            try
            {
                agent = CreateAgent(agentDef, apiKey, instructions, tools, skillDirectories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create agent for definition {AgentId}", agentDef.Id);
                return AgentChatResponse.Error($"Failed to create agent: {ex.Message}");
            }

            // Load prior messages and build ChatMessage list for conversation memory
            var chatMessages = await BuildChatHistory(dbContext, conversationId);

            // Add the new user message to the history
            chatMessages.Add(new ChatMessage(ChatRole.User, userMessage));

            // Store user message in DB
            var userMsg = new AgentConversationMessage
            {
                Id = Guid.NewGuid(),
                AgentConversationId = conversationId,
                Role = "user",
                Content = userMessage,
                DateCreated = DateTimeOffset.UtcNow,
                CreatedBy = userId
            };
            await dbContext.AgentConversationMessages.AddAsync(userMsg);
            await dbContext.SaveChangesAsync();

            // Create session and run the agent with full conversation history
            AgentSession session = await agent.CreateSessionAsync();

            AgentResponse agentResponse;
            try
            {
                agentResponse = await agent.RunAsync(chatMessages, session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent run failed for conversation {ConversationId}", conversationId);
                return AgentChatResponse.Error($"Agent invocation failed: {ex.Message}");
            }

            string responseText = agentResponse.Text ?? string.Empty;

            // Store assistant response
            var assistantMsg = new AgentConversationMessage
            {
                Id = Guid.NewGuid(),
                AgentConversationId = conversationId,
                Role = "assistant",
                Content = responseText,
                DateCreated = DateTimeOffset.UtcNow
            };
            await dbContext.AgentConversationMessages.AddAsync(assistantMsg);

            // Update conversation timestamp
            conversation.DateUpdated = DateTimeOffset.UtcNow;
            dbContext.AgentConversations.Update(conversation);

            await dbContext.SaveChangesAsync();

            return new AgentChatResponse
            {
                Success = true,
                Message = responseText,
                MessageId = assistantMsg.Id,
                ConversationId = conversationId
            };
        }

        /// <summary>
        /// Send a user message and stream the response token-by-token.
        /// </summary>
        public async IAsyncEnumerable<string> ChatStreamAsync(Guid userId, Guid conversationId, string userMessage)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();

            var conversation = await dbContext.AgentConversations
                .FirstOrDefaultAsync(x => x.Id == conversationId && x.UserId == userId && x.DateDeleted == null);

            if (conversation is null)
            {
                yield return "[ERROR] Conversation not found.";
                yield break;
            }

            var agentDef = await dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == conversation.AgentDefinitionId && x.UserId == userId && x.DateDeleted == null);

            if (agentDef is null || !agentDef.IsActive)
            {
                yield return "[ERROR] Agent not found or inactive.";
                yield break;
            }

            string? apiKey = null;
            if (!string.IsNullOrWhiteSpace(agentDef.ApiKey))
            {
                apiKey = _encryptionService.Decrypt(agentDef.ApiKey);
            }

            string instructions = await BuildInstructions(dbContext, agentDef);
            var tools = await BuildTools(dbContext, agentDef.Id, userId);
            var skillDirectories = await BuildSkillDirectories(dbContext, agentDef.Id);

            AIAgent agent;
            string? createError = null;
            try
            {
                agent = CreateAgent(agentDef, apiKey, instructions, tools, skillDirectories);
            }
            catch (Exception ex)
            {
                createError = $"[ERROR] Failed to create agent: {ex.Message}";
                agent = null!;
            }

            if (createError is not null)
            {
                yield return createError;
                yield break;
            }

            // Build chat history and add user message
            var chatMessages = await BuildChatHistory(dbContext, conversationId);
            chatMessages.Add(new ChatMessage(ChatRole.User, userMessage));

            // Store user message in DB
            var userMsg = new AgentConversationMessage
            {
                Id = Guid.NewGuid(),
                AgentConversationId = conversationId,
                Role = "user",
                Content = userMessage,
                DateCreated = DateTimeOffset.UtcNow,
                CreatedBy = userId
            };
            await dbContext.AgentConversationMessages.AddAsync(userMsg);
            await dbContext.SaveChangesAsync();

            AgentSession session = await agent.CreateSessionAsync();

            // Stream response — collect full text for DB storage
            var fullResponse = new System.Text.StringBuilder();

            await foreach (var update in agent.RunStreamingAsync(chatMessages, session))
            {
                string? chunk = update.Text;
                if (!string.IsNullOrEmpty(chunk))
                {
                    fullResponse.Append(chunk);
                    yield return chunk;
                }
            }

            // Store assistant response
            var assistantMsg = new AgentConversationMessage
            {
                Id = Guid.NewGuid(),
                AgentConversationId = conversationId,
                Role = "assistant",
                Content = fullResponse.ToString(),
                DateCreated = DateTimeOffset.UtcNow
            };
            await dbContext.AgentConversationMessages.AddAsync(assistantMsg);
            await dbContext.SaveChangesAsync();
        }

        // ── Private helpers ──────────────────────────────────────────────

        /// <summary>
        /// Build conversation history as ChatMessage objects from stored DB messages.
        /// </summary>
        private static async Task<List<ChatMessage>> BuildChatHistory(DataContext dbContext, Guid conversationId)
        {
            var dbMessages = await dbContext.AgentConversationMessages
                .Where(x => x.AgentConversationId == conversationId && x.DateDeleted == null)
                .OrderBy(x => x.DateCreated)
                .ToListAsync();

            var messages = new List<ChatMessage>();
            foreach (var msg in dbMessages)
            {
                var role = msg.Role switch
                {
                    "assistant" => ChatRole.Assistant,
                    "system" => ChatRole.System,
                    _ => ChatRole.User
                };
                messages.Add(new ChatMessage(role, msg.Content));
            }

            return messages;
        }

        /// <summary>
        /// Build the system instructions by combining agent instructions with knowledge items.
        /// </summary>
        private static async Task<string> BuildInstructions(DataContext dbContext, AgentDefinition agentDef)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(agentDef.Instructions))
            {
                parts.Add(agentDef.Instructions);
            }

            // Load enabled knowledge items
            var knowledgeItems = await dbContext.AgentKnowledgeItems
                .Where(x => x.AgentDefinitionId == agentDef.Id && x.IsEnabled && x.DateDeleted == null)
                .OrderBy(x => x.DateCreated)
                .ToListAsync();

            if (knowledgeItems.Count > 0)
            {
                parts.Add("\n\n## Knowledge Base\n");
                foreach (var item in knowledgeItems)
                {
                    parts.Add($"### {item.Title}\n{item.Content}\n");
                }
            }

            return string.Join("\n", parts);
        }

        /// <summary>
        /// Build AITool list from the agent's tool definitions.
        /// Built-in tools are real executable functions.
        /// MCP tools proxy calls to their respective MCP server endpoints.
        /// </summary>
        private async Task<IList<AITool>> BuildTools(DataContext dbContext, Guid agentId, Guid callerUserId)
        {
            var tools = new List<AITool>();

            var toolDefs = await dbContext.AgentTools
                .Where(x => x.AgentDefinitionId == agentId && x.IsEnabled && x.DateDeleted == null)
                .ToListAsync();

            // Separate built-in and MCP tools
            var builtinToolNames = toolDefs
                .Where(t => t.Source == jihadkhawaja.chat.shared.Models.AgentToolSource.Builtin)
                .Select(t => t.Name)
                .ToList();

            var mcpToolDefs = toolDefs
                .Where(t => t.Source == jihadkhawaja.chat.shared.Models.AgentToolSource.Mcp && t.McpServerId.HasValue)
                .ToList();

            // Add enabled built-in tools
            if (builtinToolNames.Count > 0)
            {
                var builtinTools = BuiltinTools.CreateTools(builtinToolNames);
                tools.AddRange(builtinTools);
            }

            // Add MCP tools — group by server to minimize lookups
            if (mcpToolDefs.Count > 0)
            {
                var serverIds = mcpToolDefs.Select(t => t.McpServerId!.Value).Distinct().ToList();
                var servers = await dbContext.Set<jihadkhawaja.chat.shared.Models.AgentMcpServer>()
                    .Where(s => serverIds.Contains(s.Id) && s.IsActive && s.DateDeleted == null)
                    .ToListAsync();

                var serverMap = servers.ToDictionary(s => s.Id);

                var mcpProxies = new List<McpToolProxy>();
                foreach (var toolDef in mcpToolDefs)
                {
                    if (serverMap.TryGetValue(toolDef.McpServerId!.Value, out var server))
                    {
                        string? decryptedKey = null;
                        if (!string.IsNullOrWhiteSpace(server.ApiKey))
                        {
                            try { decryptedKey = _encryptionService.Decrypt(server.ApiKey); }
                            catch { /* skip if decryption fails */ }
                        }

                        mcpProxies.Add(new McpToolProxy
                        {
                            Endpoint = server.Endpoint,
                            ApiKey = decryptedKey,
                            ToolName = toolDef.Name,
                            Description = toolDef.Description
                        });
                    }
                }

                if (mcpProxies.Count > 0)
                {
                    var mcpTools = _mcpClientService.CreateMcpAITools(mcpProxies);
                    tools.AddRange(mcpTools);
                }
            }

            // Add scoped agent interaction tools (search agents, add friend, add to channel)
            var scopedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "search_published_agents", "add_agent_friend", "add_agent_to_channel"
            };

            var enabledScopedTools = toolDefs
                .Where(t => t.Source == jihadkhawaja.chat.shared.Models.AgentToolSource.Builtin && scopedToolNames.Contains(t.Name))
                .Select(t => t.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (enabledScopedTools.Count > 0)
            {
                var scopedTools = BuiltinTools.CreateScopedTools(_scopeFactory, callerUserId);
                tools.AddRange(scopedTools.Where(t => t is AIFunction f && enabledScopedTools.Contains(f.Name)));
            }

            return tools;
        }

        private static async Task<List<AgentSkillDirectory>> BuildSkillDirectories(DataContext dbContext, Guid agentId)
        {
            return await dbContext.AgentSkillDirectories
                .Where(x => x.AgentDefinitionId == agentId && x.IsEnabled && x.DateDeleted == null)
                .OrderBy(x => x.DateCreated)
                .ToListAsync();
        }

        /// <summary>
        /// Create an AIAgent instance based on the provider configuration.
        /// Uses Microsoft Agent Framework extension methods for each provider.
        /// </summary>
        private AIAgent CreateAgent(AgentDefinition agentDef, string? apiKey, string instructions, IList<AITool> tools, IReadOnlyList<AgentSkillDirectory> skillDirectories)
        {
            var contextProviders = _agentSkillsService.CreateContextProviders(skillDirectories, agentDef.SkillsInstructionPrompt);
            return CreateAgentStatic(agentDef, apiKey, instructions, tools, contextProviders, _loggerFactory, _serviceProvider);
        }

        /// <summary>
        /// Public static factory for creating an AIAgent. Used by AgentChannelService for channel mentions.
        /// </summary>
        internal static AIAgent CreateAgentStatic(
            AgentDefinition agentDef,
            string? apiKey,
            string instructions,
            IList<AITool> tools,
            IReadOnlyList<AIContextProvider>? contextProviders = null,
            ILoggerFactory? loggerFactory = null,
            IServiceProvider? serviceProvider = null)
        {
            return agentDef.Provider switch
            {
                LlmProvider.OpenAI => CreateOpenAIAgent(agentDef, apiKey, instructions, tools, contextProviders, loggerFactory, serviceProvider),
                LlmProvider.AzureOpenAI => CreateAzureOpenAIAgent(agentDef, apiKey, instructions, tools, contextProviders, loggerFactory, serviceProvider),
                LlmProvider.Anthropic => CreateAnthropicAgent(agentDef, apiKey, instructions, tools, contextProviders, loggerFactory, serviceProvider),
                LlmProvider.Ollama => CreateOllamaAgent(agentDef, instructions, tools, contextProviders, loggerFactory, serviceProvider),
                _ => throw new NotSupportedException($"Provider {agentDef.Provider} is not supported.")
            };
        }

        private static AIAgent CreateOpenAIAgent(
            AgentDefinition agentDef,
            string? apiKey,
            string instructions,
            IList<AITool> tools,
            IReadOnlyList<AIContextProvider>? contextProviders,
            ILoggerFactory? loggerFactory,
            IServiceProvider? serviceProvider)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("OpenAI requires an API key.");
            }

            var client = new OpenAIClient(apiKey);
            var chatClient = client.GetChatClient(agentDef.Model);

            return OpenAI.Chat.OpenAIChatClientExtensions.AsAIAgent(
                chatClient,
                CreateAgentOptions(agentDef, instructions, tools, contextProviders),
                null,
                loggerFactory,
                serviceProvider);
        }

        private static AIAgent CreateAzureOpenAIAgent(
            AgentDefinition agentDef,
            string? apiKey,
            string instructions,
            IList<AITool> tools,
            IReadOnlyList<AIContextProvider>? contextProviders,
            ILoggerFactory? loggerFactory,
            IServiceProvider? serviceProvider)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Azure OpenAI requires an API key.");
            }

            if (string.IsNullOrWhiteSpace(agentDef.Endpoint))
            {
                throw new InvalidOperationException("Azure OpenAI requires an endpoint URL.");
            }

            var credential = new System.ClientModel.ApiKeyCredential(apiKey);
            var client = new Azure.AI.OpenAI.AzureOpenAIClient(new Uri(agentDef.Endpoint), credential);
            var chatClient = client.GetChatClient(agentDef.Model);

            return OpenAI.Chat.OpenAIChatClientExtensions.AsAIAgent(
                chatClient,
                CreateAgentOptions(agentDef, instructions, tools, contextProviders),
                null,
                loggerFactory,
                serviceProvider);
        }

        private static AIAgent CreateAnthropicAgent(
            AgentDefinition agentDef,
            string? apiKey,
            string instructions,
            IList<AITool> tools,
            IReadOnlyList<AIContextProvider>? contextProviders,
            ILoggerFactory? loggerFactory,
            IServiceProvider? serviceProvider)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Anthropic requires an API key.");
            }

            Anthropic.IAnthropicClient client = new Anthropic.AnthropicClient() { ApiKey = apiKey };

            return Anthropic.AnthropicClientExtensions.AsAIAgent(
                client,
                CreateAgentOptions(agentDef, instructions, tools, contextProviders),
                null,
                loggerFactory,
                serviceProvider);
        }

        private static AIAgent CreateOllamaAgent(
            AgentDefinition agentDef,
            string instructions,
            IList<AITool> tools,
            IReadOnlyList<AIContextProvider>? contextProviders,
            ILoggerFactory? loggerFactory,
            IServiceProvider? serviceProvider)
        {
            var endpoint = string.IsNullOrWhiteSpace(agentDef.Endpoint)
                ? "http://localhost:11434"
                : agentDef.Endpoint;

            var chatClient = new OllamaChatClient(
                new Uri(endpoint),
                modelId: agentDef.Model);

            return chatClient.AsAIAgent(
                CreateAgentOptions(agentDef, instructions, tools, contextProviders),
                loggerFactory,
                serviceProvider);
        }

        private static ChatClientAgentOptions CreateAgentOptions(
            AgentDefinition agentDef,
            string instructions,
            IList<AITool> tools,
            IReadOnlyList<AIContextProvider>? contextProviders)
        {
            return new ChatClientAgentOptions
            {
                Name = agentDef.Name,
                Description = agentDef.Description,
                ChatOptions = new ChatOptions
                {
                    Instructions = instructions,
                    Tools = tools.Count > 0 ? tools : null,
                    Temperature = agentDef.Temperature,
                    MaxOutputTokens = agentDef.MaxTokens
                },
                AIContextProviders = contextProviders is { Count: > 0 }
                    ? contextProviders.ToArray()
                    : null
            };
        }
    }

    /// <summary>
    /// Response model for agent chat operations.
    /// </summary>
    public class AgentChatResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid? MessageId { get; set; }
        public Guid? ConversationId { get; set; }

        public static AgentChatResponse Error(string message) => new()
        {
            Success = false,
            Message = message
        };
    }
}
