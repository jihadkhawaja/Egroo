using Egroo.Server.Database;
using Egroo.Server.Services;
using jihadkhawaja.chat.shared.Models;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text;

namespace Egroo.Server.Tools
{
    internal static class A2ATool
    {
        private static readonly AsyncLocal<int> RelayDepth = new();

        public static readonly ISet<string> ScopedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "discover_agent_friends",
            "message_agent_friend"
        };

        public static IReadOnlyList<BuiltinToolDefinition> GetDefinitions() =>
        [
            new()
            {
                Name = "discover_agent_friends",
                Description = "Discover published friend agents the current user has already added. Use this before agent-to-agent collaboration so the calling agent can find other available agents.",
                ParametersSchema = """{"type":"object","properties":{"query":{"type":"string","description":"Optional search term to filter friend agents by name or description"}},"required":[]}"""
            },
            new()
            {
                Name = "message_agent_friend",
                Description = "Send a direct message to a friend agent and return its response. Use this when an agent needs to collaborate with another published friend agent.",
                ParametersSchema = """{"type":"object","properties":{"agent_id":{"type":"string","description":"The unique ID (GUID) of the friend agent to message"},"message":{"type":"string","description":"The message to send to the friend agent"}},"required":["agent_id","message"]}"""
            }
        ];

        public static IList<AITool> CreateScopedTools(IServiceScopeFactory scopeFactory, Guid callerUserId)
        {
            return new List<AITool>
            {
                AIFunctionFactory.Create(
                    ([Description("Optional search term to filter friend agents by name or description")] string? query = null) =>
                        DiscoverAgentFriendsAsync(scopeFactory, callerUserId, query),
                    "discover_agent_friends",
                    "Discover published friend agents the current user has already added."),
                AIFunctionFactory.Create(
                    ([Description("The unique ID (GUID) of the friend agent to message")] string agent_id,
                     [Description("The message to send to the friend agent")] string message) =>
                        MessageAgentFriendAsync(scopeFactory, callerUserId, agent_id, message),
                    "message_agent_friend",
                    "Send a direct message to a friend agent and return its response.")
            };
        }

        private static async Task<string> DiscoverAgentFriendsAsync(IServiceScopeFactory scopeFactory, Guid callerUserId, string? query)
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
            var agents = await EgrooTool.GetFriendAgentsAsync(dbContext, callerUserId);

            var filtered = string.IsNullOrWhiteSpace(query)
                ? agents
                : agents.Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(x.Description) && x.Description.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

            if (filtered.Count == 0)
            {
                return string.IsNullOrWhiteSpace(query)
                    ? "No friend agents are currently available. Add published agents as friends first."
                    : $"No friend agents matched '{query}'.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {filtered.Count} friend agent(s):");
            foreach (var agent in filtered)
            {
                sb.AppendLine($"  - {agent.Name} (ID: {agent.Id})");
                if (!string.IsNullOrWhiteSpace(agent.Description))
                {
                    sb.AppendLine($"    Description: {agent.Description}");
                }

                sb.AppendLine($"    Provider: {agent.Provider}, Model: {agent.Model}");
            }

            return sb.ToString();
        }

        private static async Task<string> MessageAgentFriendAsync(IServiceScopeFactory scopeFactory, Guid callerUserId, string agentIdStr, string message)
        {
            if (!Guid.TryParse(agentIdStr, out var agentId))
            {
                return "Invalid agent ID format. Please provide a valid GUID.";
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return "Message content is required.";
            }

            if (RelayDepth.Value > 0)
            {
                return "Nested agent-to-agent relays are blocked to prevent runaway loops.";
            }

            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
            var runtimeService = scope.ServiceProvider.GetRequiredService<AgentRuntimeService>();

            var targetAgent = await EgrooTool.GetAccessibleFriendAgentAsync(dbContext, callerUserId, agentId);
            if (targetAgent is null)
            {
                return "The target agent is not accessible. Ensure it is active and added as a friend first.";
            }

            var conversation = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                dbContext.AgentConversations.OrderByDescending(x => x.DateUpdated ?? x.DateCreated),
                x => x.AgentDefinitionId == agentId && x.UserId == callerUserId && x.DateDeleted == null);

            if (conversation is null)
            {
                conversation = new AgentConversation
                {
                    Id = Guid.NewGuid(),
                    AgentDefinitionId = agentId,
                    UserId = callerUserId,
                    Title = $"A2A: {targetAgent.Name}",
                    DateCreated = DateTimeOffset.UtcNow,
                    CreatedBy = callerUserId
                };

                await dbContext.AgentConversations.AddAsync(conversation);
                await dbContext.SaveChangesAsync();
            }

            RelayDepth.Value++;
            try
            {
                var response = await runtimeService.ChatAsync(callerUserId, conversation.Id, new AgentChatRequest
                {
                    Message = message,
                    DisplayMessage = message
                });

                if (!response.Success)
                {
                    return $"Agent-to-agent relay failed: {response.Message}";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Agent: {targetAgent.Name}");
                sb.AppendLine($"Conversation ID: {conversation.Id}");
                sb.AppendLine();
                sb.AppendLine(response.Message ?? string.Empty);
                return sb.ToString().TrimEnd();
            }
            finally
            {
                RelayDepth.Value--;
            }
        }
    }
}