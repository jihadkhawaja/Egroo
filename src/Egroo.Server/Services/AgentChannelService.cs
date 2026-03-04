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
        private readonly EncryptionService _encryptionService;
        private readonly McpClientService _mcpClientService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IConnectionTracker _connectionTracker;
        private readonly ILogger<AgentChannelService> _logger;

        /// <summary>
        /// Raised when an agent produces a response message that should be sent to the channel.
        /// The hub subscribes to this to broadcast the agent message via SignalR.
        /// </summary>
        public event Func<Message, Task>? OnAgentResponse;

        public AgentChannelService(
            IServiceScopeFactory scopeFactory,
            EncryptionService encryptionService,
            McpClientService mcpClientService,
            IHubContext<ChatHub> hubContext,
            IConnectionTracker connectionTracker,
            ILogger<AgentChannelService> logger)
        {
            _scopeFactory = scopeFactory;
            _encryptionService = encryptionService;
            _mcpClientService = mcpClientService;
            _hubContext = hubContext;
            _connectionTracker = connectionTracker;
            _logger = logger;
        }

        /// <summary>
        /// Detect @mentions for channel agents and trigger responses asynchronously.
        /// Called after a message is successfully sent in a channel.
        /// </summary>
        public async Task ProcessMentionsAsync(Message message)
        {
            if (string.IsNullOrWhiteSpace(message.Content))
            {
                return;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                // Get all agents in this channel
                var channelAgents = await dbContext.ChannelAgents
                    .Where(x => x.ChannelId == message.ChannelId && x.DateDeleted == null)
                    .Select(x => x.AgentDefinitionId)
                    .ToListAsync();

                if (channelAgents.Count == 0)
                {
                    return;
                }

                var agentDefs = await dbContext.AgentDefinitions
                    .Where(x => channelAgents.Contains(x.Id) && x.IsActive && x.DateDeleted == null)
                    .ToListAsync();

                if (agentDefs.Count == 0)
                {
                    return;
                }

                // Find mentioned agents by @name or @<name> pattern (case-insensitive)
                var mentionedAgents = new List<AgentDefinition>();
                foreach (var agent in agentDefs)
                {
                    // Match @<Agent Name> (bracket syntax for names with spaces) or @AgentName (no spaces)
                    var escapedName = Regex.Escape(agent.Name);
                    var bracketPattern = $@"@<{escapedName}>";
                    var plainPattern = $@"@{escapedName}(?:\b|$)";
                    if (Regex.IsMatch(message.Content, bracketPattern, RegexOptions.IgnoreCase)
                        || Regex.IsMatch(message.Content, plainPattern, RegexOptions.IgnoreCase))
                    {
                        mentionedAgents.Add(agent);
                    }
                }

                if (mentionedAgents.Count == 0)
                {
                    return;
                }

                // Process each mentioned agent (fire-and-forget per agent)
                foreach (var agentDef in mentionedAgents)
                {
                    _ = Task.Run(() => GenerateAgentResponseAsync(agentDef, message));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing mentions in channel {ChannelId}", message.ChannelId);
            }
        }

        private async Task GenerateAgentResponseAsync(AgentDefinition agentDef, Message triggerMessage)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();

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
                        return;
                    }
                }

                // Build instructions with knowledge
                string instructions = await BuildInstructions(dbContext, agentDef);

                // Add channel context to instructions
                instructions += "\n\n## Channel Context\n";
                instructions += "You are participating in a group chat channel. ";
                instructions += "You were mentioned with @" + agentDef.Name + ". ";
                instructions += "Respond naturally to the conversation. Keep your response concise and relevant.";

                // Build tools
                var tools = await BuildTools(dbContext, agentDef.Id, agentDef.UserId);

                // Create agent
                AIAgent agent;
                try
                {
                    agent = AgentRuntimeService.CreateAgentStatic(agentDef, apiKey, instructions, tools);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create agent for channel mention {AgentId}", agentDef.Id);
                    return;
                }

                // Build context from recent channel messages
                var recentMessages = await dbContext.Messages
                    .Where(x => x.ChannelId == triggerMessage.ChannelId && x.DateDeleted == null)
                    .OrderByDescending(x => x.DateSent)
                    .Take(20)
                    .OrderBy(x => x.DateSent)
                    .ToListAsync();

                var chatMessages = new List<ChatMessage>();
                foreach (var msg in recentMessages)
                {
                    // Load display names and decrypt content from pending messages
                    var pendingMsg = await dbContext.UsersPendingMessages
                        .FirstOrDefaultAsync(x => x.MessageId == msg.Id && x.DateDeleted == null);

                    string? content = pendingMsg?.Content;
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        try { content = _encryptionService.Decrypt(content); }
                        catch { /* use as-is if decryption fails */ }
                    }

                    if (string.IsNullOrWhiteSpace(content))
                    {
                        continue;
                    }

                    // Determine sender name
                    string senderName;
                    if (msg.AgentDefinitionId.HasValue)
                    {
                        var senderAgent = await dbContext.AgentDefinitions
                            .FirstOrDefaultAsync(x => x.Id == msg.AgentDefinitionId.Value);
                        senderName = senderAgent?.Name ?? "Agent";
                        chatMessages.Add(new ChatMessage(ChatRole.Assistant, $"[{senderName}]: {content}"));
                    }
                    else
                    {
                        var sender = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == msg.SenderId);
                        senderName = sender?.Username ?? "Unknown";

                        if (msg.Id == triggerMessage.Id)
                        {
                            chatMessages.Add(new ChatMessage(ChatRole.User, $"[{senderName}]: {content}"));
                        }
                        else
                        {
                            chatMessages.Add(new ChatMessage(ChatRole.User, $"[{senderName}]: {content}"));
                        }
                    }
                }

                // Run the agent
                AgentSession session = await agent.CreateSessionAsync();
                AgentResponse agentResponse;
                try
                {
                    agentResponse = await agent.RunAsync(chatMessages, session);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Agent run failed for channel mention {AgentId}", agentDef.Id);
                    return;
                }

                string responseText = agentResponse.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(responseText))
                {
                    return;
                }

                // Create the response message
                var responseMessage = new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = agentDef.UserId, // Agent owner's user ID for compatibility
                    ChannelId = triggerMessage.ChannelId,
                    ReferenceId = Guid.NewGuid(),
                    DateSent = DateTimeOffset.UtcNow,
                    DateCreated = DateTimeOffset.UtcNow,
                    AgentDefinitionId = agentDef.Id,
                    Content = responseText,
                    DisplayName = agentDef.Name
                };

                // Save to database
                await dbContext.Messages.AddAsync(responseMessage);
                await dbContext.SaveChangesAsync();

                // Create encrypted pending messages and broadcast to channel members
                await BroadcastAgentMessage(dbContext, responseMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating agent response for {AgentId} in channel {ChannelId}",
                    agentDef.Id, triggerMessage.ChannelId);
            }
        }

        private async Task BroadcastAgentMessage(DataContext dbContext, Message message)
        {
            // Get all users in the channel
            var channelUserIds = await dbContext.ChannelUsers
                .Where(x => x.ChannelId == message.ChannelId)
                .Select(x => x.UserId)
                .ToListAsync();

            // Create pending messages and send via SignalR for each online member
            foreach (var userId in channelUserIds)
            {
                var pendingMsg = new UserPendingMessage
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    MessageId = message.Id,
                    Content = _encryptionService.Encrypt(message.Content!),
                    DateCreated = DateTimeOffset.UtcNow
                };

                await dbContext.UsersPendingMessages.AddAsync(pendingMsg);

                // Send real-time notification to online users
                var connectionIds = _connectionTracker.GetUserConnectionIds(userId);
                if (connectionIds.Count > 0)
                {
                    var lastConnection = connectionIds.Last();
                    try
                    {
                        await _hubContext.Clients.Client(lastConnection).SendAsync("ReceiveMessage", message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send agent message to user {UserId}", userId);
                    }
                }
            }

            await dbContext.SaveChangesAsync();
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

            var scopedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "search_published_agents", "add_agent_friend", "add_agent_to_channel"
            };

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
    }
}
