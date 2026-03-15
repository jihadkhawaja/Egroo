using Egroo.Server.Database;
using Egroo.Server.Security;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using jihadkhawaja.chat.server.Hubs;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenAI;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Egroo.Server.Services
{
    /// <summary>
    /// Handles agent participation in channels: mention detection and response generation.
    /// When a message containing @AgentName is sent in a channel that has agents,
    /// this service triggers the mentioned agent to produce a response.
    /// </summary>
    public class AgentChannelService : IAgentChannelResponder
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AgentSkillsService _agentSkillsService;
        private readonly EncryptionService _encryptionService;
        private readonly EndToEndEncryptionService _endToEndEncryptionService;
        private readonly McpClientService _mcpClientService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IConnectionTracker _connectionTracker;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AgentChannelService> _logger;
        private readonly ConcurrentDictionary<string, int> _activeTypingStates = new();

        public AgentChannelService(
            IServiceScopeFactory scopeFactory,
            AgentSkillsService agentSkillsService,
            EncryptionService encryptionService,
            EndToEndEncryptionService endToEndEncryptionService,
            McpClientService mcpClientService,
            IHubContext<ChatHub> hubContext,
            IConnectionTracker connectionTracker,
            ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider,
            ILogger<AgentChannelService> logger)
        {
            _scopeFactory = scopeFactory;
            _agentSkillsService = agentSkillsService;
            _encryptionService = encryptionService;
            _endToEndEncryptionService = endToEndEncryptionService;
            _mcpClientService = mcpClientService;
            _hubContext = hubContext;
            _connectionTracker = connectionTracker;
            _loggerFactory = loggerFactory;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Detect @mentions for channel agents and trigger responses asynchronously.
        /// Called after a message is successfully sent in a channel.
        /// </summary>
        public async Task PersistAgentRecipientContentsAsync(Message message)
        {
            if (message.Id == Guid.Empty || message.AgentRecipientContents is not { Count: > 0 })
            {
                return;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                var agentContents = message.AgentRecipientContents
                    .Where(x => x.AgentDefinitionId != Guid.Empty && !string.IsNullOrWhiteSpace(x.Content))
                    .GroupBy(x => x.AgentDefinitionId)
                    .Select(x => x.First())
                    .ToArray();

                if (agentContents.Length == 0)
                {
                    return;
                }

                var existingIds = await dbContext.AgentPendingMessages
                    .Where(x => x.MessageId == message.Id)
                    .Select(x => x.AgentDefinitionId)
                    .ToListAsync();

                foreach (var agentContent in agentContents)
                {
                    if (existingIds.Contains(agentContent.AgentDefinitionId))
                    {
                        continue;
                    }

                    await dbContext.AgentPendingMessages.AddAsync(new AgentPendingMessage
                    {
                        Id = Guid.NewGuid(),
                        AgentDefinitionId = agentContent.AgentDefinitionId,
                        MessageId = message.Id,
                        Content = agentContent.Content,
                        DateCreated = DateTimeOffset.UtcNow
                    });
                }

                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing agent recipient envelopes for message {MessageId}", message.Id);
            }
        }

        public async Task ProcessMentionsAsync(Message message)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                var mentionedAgents = await GetMentionedAgentsAsync(dbContext, message);
                QueueAgentResponses(mentionedAgents, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing mentions in channel {ChannelId}", message.ChannelId);
            }
        }

        private async Task GenerateAgentResponseAsync(AgentDefinition agentDef, Message triggerMessage, string triggerContent)
        {
            ChannelTypingState? typingState = null;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                typingState = CreateTypingState(agentDef, triggerMessage.ChannelId);
                await StartTypingAsync(dbContext, typingState);

                var runContext = await BuildAgentRunContextAsync(dbContext, agentDef, triggerMessage);
                if (runContext is null)
                {
                    return;
                }

                var agent = TryCreateRuntimeAgent(agentDef, runContext);
                if (agent is null)
                {
                    return;
                }

                var chatMessages = await BuildChannelChatMessagesAsync(dbContext, triggerMessage, triggerContent, agentDef, runContext.AgentPrivateKey);
                string responseText = await RunChannelAgentAsync(agent, chatMessages, agentDef.Name, agentDef.Id);
                if (string.IsNullOrWhiteSpace(responseText))
                {
                    return;
                }

                var responseMessage = CreateAgentResponseMessage(agentDef, triggerMessage.ChannelId, responseText);
                await SaveAndBroadcastAgentMessageAsync(dbContext, responseMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating agent response for {AgentId} in channel {ChannelId}",
                    agentDef.Id, triggerMessage.ChannelId);
            }
            finally
            {
                if (typingState is not null)
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                        await StopTypingAsync(dbContext, typingState);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to stop typing indicator for agent {AgentId}", agentDef.Id);
                    }
                }
            }
        }

        private async Task<List<(AgentDefinition Agent, string TriggerContent)>> GetMentionedAgentsAsync(DataContext dbContext, Message message)
        {
            var agentDefs = await GetActiveChannelAgentsAsync(dbContext, message.ChannelId);
            if (agentDefs.Count == 0)
            {
                return [];
            }

            var mentionedAgents = new List<(AgentDefinition Agent, string TriggerContent)>();
            foreach (var agent in agentDefs)
            {
                var triggerContent = GetAgentMessageContent(message, agent);
                if (!IsAgentMentioned(agent, triggerContent, out var normalizedContent))
                {
                    continue;
                }

                mentionedAgents.Add((agent, normalizedContent));
            }

            return mentionedAgents;
        }

        private async Task<List<AgentDefinition>> GetActiveChannelAgentsAsync(DataContext dbContext, Guid channelId)
        {
            var channelAgentIds = await dbContext.ChannelAgents
                .Where(x => x.ChannelId == channelId && x.DateDeleted == null)
                .Select(x => x.AgentDefinitionId)
                .ToListAsync();

            if (channelAgentIds.Count == 0)
            {
                return [];
            }

            return await dbContext.AgentDefinitions
                .Where(x => channelAgentIds.Contains(x.Id) && x.IsActive && x.DateDeleted == null)
                .ToListAsync();
        }

        private static bool IsAgentMentioned(AgentDefinition agent, string? triggerContent, out string normalizedContent)
        {
            normalizedContent = string.Empty;
            if (string.IsNullOrWhiteSpace(triggerContent))
            {
                return false;
            }

            normalizedContent = triggerContent;
            string readableTriggerContent = AgentAttachmentPromptFormatter.Normalize(triggerContent);
            var escapedName = Regex.Escape(agent.Name);

            return Regex.IsMatch(readableTriggerContent, $@"@<{escapedName}>", RegexOptions.IgnoreCase)
                || Regex.IsMatch(readableTriggerContent, $@"@{escapedName}(?:\b|$)", RegexOptions.IgnoreCase);
        }

        private void QueueAgentResponses(IEnumerable<(AgentDefinition Agent, string TriggerContent)> mentionedAgents, Message message)
        {
            foreach (var mentionedAgent in mentionedAgents)
            {
                _ = Task.Run(() => GenerateAgentResponseAsync(mentionedAgent.Agent, message, mentionedAgent.TriggerContent));
            }
        }

        private static ChannelTypingState CreateTypingState(AgentDefinition agentDef, Guid channelId)
        {
            return new ChannelTypingState
            {
                ChannelId = channelId,
                UserId = agentDef.UserId,
                AgentDefinitionId = agentDef.Id,
                DisplayName = agentDef.Name,
                IsAgent = true
            };
        }

        private async Task<AgentRunContext?> BuildAgentRunContextAsync(DataContext dbContext, AgentDefinition agentDef, Message triggerMessage)
        {
            var apiKey = TryDecryptAgentApiKey(agentDef);
            if (agentDef.ApiKey is not null && apiKey is null)
            {
                return null;
            }

            var agentPrivateKey = GetAgentPrivateKey(agentDef, triggerMessage.AgentRecipientContents is not null);
            if (triggerMessage.AgentRecipientContents is not null && string.IsNullOrWhiteSpace(agentPrivateKey))
            {
                return null;
            }

            var instructions = await BuildChannelInstructionsAsync(dbContext, agentDef);
            var tools = await BuildTools(dbContext, agentDef.Id, agentDef.UserId);
            var contextProviders = await BuildContextProvidersAsync(dbContext, agentDef.Id, agentDef.SkillsInstructionPrompt);

            return new AgentRunContext(apiKey, agentPrivateKey, instructions, tools, contextProviders);
        }

        private string? TryDecryptAgentApiKey(AgentDefinition agentDef)
        {
            if (string.IsNullOrWhiteSpace(agentDef.ApiKey))
            {
                return null;
            }

            try
            {
                return _encryptionService.Decrypt(agentDef.ApiKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt API key for agent {AgentId}", agentDef.Id);
                return null;
            }
        }

        private string? GetAgentPrivateKey(AgentDefinition agentDef, bool requiresKey)
        {
            string? agentPrivateKey = _endToEndEncryptionService.DecryptAgentPrivateKey(agentDef.EncryptionPrivateKey);
            if (requiresKey && string.IsNullOrWhiteSpace(agentPrivateKey))
            {
                _logger.LogWarning("Agent {AgentId} has no private key available for encrypted channel processing.", agentDef.Id);
            }

            return agentPrivateKey;
        }

        private async Task<string> BuildChannelInstructionsAsync(DataContext dbContext, AgentDefinition agentDef)
        {
            var instructions = await BuildInstructions(dbContext, agentDef);
            return string.Concat(
                instructions,
                "\n\n## Channel Context\n",
                "You are participating in a group chat channel. ",
                "You were mentioned with @", agentDef.Name, ". ",
                "Respond naturally to the conversation. Keep your response concise and relevant.");
        }

        private async Task<IReadOnlyList<AIContextProvider>> BuildContextProvidersAsync(DataContext dbContext, Guid agentId, string? skillsInstructionPrompt)
        {
            var skillDirectories = await dbContext.AgentSkillDirectories
                .Where(x => x.AgentDefinitionId == agentId && x.IsEnabled && x.DateDeleted == null)
                .OrderBy(x => x.DateCreated)
                .ToListAsync();

            return _agentSkillsService.CreateContextProviders(skillDirectories, skillsInstructionPrompt);
        }

        private AIAgent? TryCreateRuntimeAgent(AgentDefinition agentDef, AgentRunContext runContext)
        {
            try
            {
                return AgentRuntimeService.CreateAgentStatic(
                    agentDef,
                    runContext.ApiKey,
                    runContext.Instructions,
                    runContext.Tools,
                    runContext.ContextProviders,
                    _loggerFactory,
                    _serviceProvider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create agent for channel mention {AgentId}", agentDef.Id);
                return null;
            }
        }

        private async Task<List<ChatMessage>> BuildChannelChatMessagesAsync(
            DataContext dbContext,
            Message triggerMessage,
            string triggerContent,
            AgentDefinition agentDef,
            string? agentPrivateKey)
        {
            var recentMessages = await dbContext.Messages
                .Where(x => x.ChannelId == triggerMessage.ChannelId && x.DateDeleted == null)
                .OrderByDescending(x => x.DateSent)
                .Take(20)
                .OrderBy(x => x.DateSent)
                .ToListAsync();

            var chatMessages = new List<ChatMessage>();
            foreach (var message in recentMessages)
            {
                var chatMessage = await BuildChatMessageAsync(dbContext, message, triggerMessage, triggerContent, agentDef, agentPrivateKey);
                if (chatMessage is not null)
                {
                    chatMessages.Add(chatMessage);
                }
            }

            return chatMessages;
        }

        private async Task<ChatMessage?> BuildChatMessageAsync(
            DataContext dbContext,
            Message message,
            Message triggerMessage,
            string triggerContent,
            AgentDefinition agentDef,
            string? agentPrivateKey)
        {
            string? content = message.Id == triggerMessage.Id
                ? triggerContent
                : await GetContentForAgentAsync(dbContext, message, agentDef, agentPrivateKey);

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            return message.AgentDefinitionId.HasValue
                ? await CreateAssistantChatMessageAsync(dbContext, message.AgentDefinitionId.Value, content)
                : await CreateUserChatMessageAsync(dbContext, message.SenderId, content);
        }

        private async Task<ChatMessage> CreateAssistantChatMessageAsync(DataContext dbContext, Guid agentDefinitionId, string content)
        {
            var senderAgent = await dbContext.AgentDefinitions.FirstOrDefaultAsync(x => x.Id == agentDefinitionId);
            var senderName = senderAgent?.Name ?? "Agent";
            return AgentChatMessageFactory.CreateChannelMessage(ChatRole.Assistant, senderName, content);
        }

        private async Task<ChatMessage> CreateUserChatMessageAsync(DataContext dbContext, Guid senderId, string content)
        {
            var sender = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == senderId);
            var senderName = sender?.Username ?? "Unknown";
            return AgentChatMessageFactory.CreateChannelMessage(ChatRole.User, senderName, content);
        }

        private async Task<string> RunChannelAgentAsync(AIAgent agent, IEnumerable<ChatMessage> chatMessages, string agentName, Guid agentId)
        {
            try
            {
                AgentSession session = await agent.CreateSessionAsync();
                var response = await agent.RunAsync(chatMessages.ToList(), session);
                return NormalizeAgentReply(response.Text, agentName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent run failed for channel mention {AgentId}", agentId);
                return string.Empty;
            }
        }

        private static Message CreateAgentResponseMessage(AgentDefinition agentDef, Guid channelId, string responseText)
        {
            return new Message
            {
                Id = Guid.NewGuid(),
                SenderId = agentDef.UserId,
                ChannelId = channelId,
                ReferenceId = Guid.NewGuid(),
                DateSent = DateTimeOffset.UtcNow,
                DateCreated = DateTimeOffset.UtcNow,
                AgentDefinitionId = agentDef.Id,
                Content = responseText,
                DisplayName = agentDef.Name
            };
        }

        private async Task SaveAndBroadcastAgentMessageAsync(DataContext dbContext, Message responseMessage)
        {
            await dbContext.Messages.AddAsync(responseMessage);
            await dbContext.SaveChangesAsync();
            await BroadcastAgentMessage(dbContext, responseMessage);
        }

        private async Task BroadcastAgentMessage(DataContext dbContext, Message message)
        {
            var channelUsers = await dbContext.ChannelUsers
                .Where(x => x.ChannelId == message.ChannelId)
                .Join(dbContext.Users,
                    channelUser => channelUser.UserId,
                    user => user.Id,
                    (_, user) => new UserDto
                    {
                        Id = user.Id,
                        Username = user.Username,
                        EncryptionPublicKey = user.EncryptionPublicKey,
                        EncryptionKeyId = user.EncryptionKeyId,
                        EncryptionKeyUpdatedOn = user.EncryptionKeyUpdatedOn
                    })
                .ToListAsync();

            var channelAgents = await dbContext.ChannelAgents
                .Where(x => x.ChannelId == message.ChannelId && x.DateDeleted == null)
                .Join(dbContext.AgentDefinitions,
                    channelAgent => channelAgent.AgentDefinitionId,
                    definition => definition.Id,
                    (_, definition) => definition)
                .Where(x => x.IsActive && x.DateDeleted == null)
                .ToListAsync();

            EndToEndEncryptionService.ChannelEncryptionResult encryptedPayload;
            try
            {
                encryptedPayload = _endToEndEncryptionService.EncryptForChannelRecipients(message.Content!, channelUsers, channelAgents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to encrypt agent response for channel {ChannelId}", message.ChannelId);
                return;
            }

            message.RecipientContents = encryptedPayload.UserRecipientContents;
            message.AgentRecipientContents = encryptedPayload.AgentRecipientContents;

            foreach (var user in channelUsers)
            {
                string? deliveryContent = encryptedPayload.UserRecipientContents.FirstOrDefault(x => x.UserId == user.Id)?.Content;
                if (string.IsNullOrWhiteSpace(deliveryContent))
                {
                    continue;
                }

                var pendingMsg = new UserPendingMessage
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    MessageId = message.Id,
                    Content = deliveryContent,
                    DateCreated = DateTimeOffset.UtcNow
                };

                await dbContext.UsersPendingMessages.AddAsync(pendingMsg);

                var connectionIds = _connectionTracker.GetUserConnectionIds(user.Id);
                if (connectionIds.Count > 0)
                {
                    var lastConnection = connectionIds.Last();
                    try
                    {
                        await _hubContext.Clients.Client(lastConnection).SendAsync("ReceiveMessage", CloneForUserDelivery(message, deliveryContent));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send agent message to user {UserId}", user.Id);
                    }
                }
            }

            foreach (var agentRecipient in encryptedPayload.AgentRecipientContents)
            {
                await dbContext.AgentPendingMessages.AddAsync(new AgentPendingMessage
                {
                    Id = Guid.NewGuid(),
                    AgentDefinitionId = agentRecipient.AgentDefinitionId,
                    MessageId = message.Id,
                    Content = agentRecipient.Content,
                    DateCreated = DateTimeOffset.UtcNow
                });
            }

            await dbContext.SaveChangesAsync();
        }

        private string? GetAgentMessageContent(Message message, AgentDefinition agent)
        {
            if (!string.IsNullOrWhiteSpace(message.Content))
            {
                return message.Content;
            }

            string? transportContent = message.AgentRecipientContents?.FirstOrDefault(x => x.AgentDefinitionId == agent.Id)?.Content;
            if (string.IsNullOrWhiteSpace(transportContent))
            {
                return null;
            }

            string? privateKey = _endToEndEncryptionService.DecryptAgentPrivateKey(agent.EncryptionPrivateKey);
            return _endToEndEncryptionService.DecryptTransportContent(transportContent, privateKey);
        }

        private async Task<string?> GetContentForAgentAsync(DataContext dbContext, Message message, AgentDefinition agentDef, string? privateKey)
        {
            var agentPending = await dbContext.AgentPendingMessages
                .FirstOrDefaultAsync(x => x.AgentDefinitionId == agentDef.Id && x.MessageId == message.Id && x.DateDeleted == null);

            if (!string.IsNullOrWhiteSpace(agentPending?.Content))
            {
                return _endToEndEncryptionService.DecryptTransportContent(agentPending.Content, privateKey);
            }

            var userPending = await dbContext.UsersPendingMessages
                .FirstOrDefaultAsync(x => x.MessageId == message.Id && x.DateDeleted == null);

            string? content = userPending?.Content;
            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    return _encryptionService.Decrypt(content);
                }
                catch
                {
                    return content;
                }
            }

            return message.Content;
        }

        private static Message CloneForUserDelivery(Message source, string deliveryContent)
        {
            return new Message
            {
                Id = source.Id,
                SenderId = source.SenderId,
                ChannelId = source.ChannelId,
                ReferenceId = source.ReferenceId,
                DateSent = source.DateSent,
                DateSeen = source.DateSeen,
                DateCreated = source.DateCreated,
                DateUpdated = source.DateUpdated,
                DateDeleted = source.DateDeleted,
                AgentDefinitionId = source.AgentDefinitionId,
                DisplayName = source.DisplayName,
                Content = deliveryContent,
            };
        }

        private async Task StartTypingAsync(DataContext dbContext, ChannelTypingState typingState)
        {
            string key = GetTypingKey(typingState);
            int activeCount = _activeTypingStates.AddOrUpdate(key, 1, (_, current) => current + 1);
            if (activeCount == 1)
            {
                await BroadcastTypingState(dbContext, typingState, "TypingStarted");
            }
        }

        private async Task StopTypingAsync(DataContext dbContext, ChannelTypingState typingState)
        {
            string key = GetTypingKey(typingState);
            int remaining = _activeTypingStates.AddOrUpdate(key, 0, (_, current) => Math.Max(current - 1, 0));
            if (remaining <= 0)
            {
                _activeTypingStates.TryRemove(key, out _);
                await BroadcastTypingState(dbContext, typingState, "TypingStopped");
            }
        }

        private async Task BroadcastTypingState(DataContext dbContext, ChannelTypingState typingState, string eventName)
        {
            var channelUserIds = await dbContext.ChannelUsers
                .Where(x => x.ChannelId == typingState.ChannelId)
                .Select(x => x.UserId)
                .ToListAsync();

            foreach (var userId in channelUserIds)
            {
                var connectionIds = _connectionTracker.GetUserConnectionIds(userId);
                if (connectionIds.Count == 0)
                {
                    continue;
                }

                try
                {
                    await _hubContext.Clients.Clients(connectionIds).SendAsync(eventName, typingState);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to send typing state {EventName} to user {UserId}", eventName, userId);
                }
            }
        }

        private static string GetTypingKey(ChannelTypingState typingState)
        {
            return typingState.AgentDefinitionId.HasValue
                ? $"agent:{typingState.AgentDefinitionId.Value}"
                : $"user:{typingState.UserId}";
        }

        private static string NormalizeAgentReply(string? responseText, string agentName)
        {
            string normalized = (responseText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            string escapedName = Regex.Escape(agentName);
            normalized = Regex.Replace(
                normalized,
                $@"^\s*(?:\[{escapedName}\]|{escapedName})\s*:\s*",
                string.Empty,
                RegexOptions.IgnoreCase);

            return normalized.Trim();
        }

        private static async Task<string> BuildInstructions(DataContext dbContext, AgentDefinition agentDef)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(agentDef.Instructions))
            {
                parts.Add(agentDef.Instructions);
            }

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

        private async Task<IList<AITool>> BuildTools(DataContext dbContext, Guid agentId, Guid callerUserId)
        {
            var tools = new List<AITool>();

            var toolDefs = await dbContext.AgentTools
                .Where(x => x.AgentDefinitionId == agentId && x.IsEnabled && x.DateDeleted == null)
                .ToListAsync();

            var builtinToolNames = toolDefs
                .Where(t => t.Source == AgentToolSource.Builtin)
                .Select(t => t.Name)
                .ToList();

            var mcpToolDefs = toolDefs
                .Where(t => t.Source == AgentToolSource.Mcp && t.McpServerId.HasValue)
                .ToList();

            if (builtinToolNames.Count > 0)
            {
                var builtinTools = BuiltinTools.CreateTools(builtinToolNames);
                tools.AddRange(builtinTools);
            }

            if (mcpToolDefs.Count > 0)
            {
                var serverIds = mcpToolDefs.Select(t => t.McpServerId!.Value).Distinct().ToList();
                var servers = await dbContext.AgentMcpServers
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
                            catch { /* skip */ }
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

            var scopedToolNames = BuiltinTools.GetScopedToolNames();

            var enabledScopedTools = toolDefs
                .Where(t => t.Source == AgentToolSource.Builtin && scopedToolNames.Contains(t.Name))
                .Select(t => t.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (enabledScopedTools.Count > 0)
            {
                var scopedTools = BuiltinTools.CreateScopedTools(_scopeFactory, callerUserId);
                tools.AddRange(scopedTools.Where(t => t is AIFunction f && enabledScopedTools.Contains(f.Name)));
            }

            return tools;
        }

        private sealed record AgentRunContext(
            string? ApiKey,
            string? AgentPrivateKey,
            string Instructions,
            IList<AITool> Tools,
            IReadOnlyList<AIContextProvider> ContextProviders);
    }
}
