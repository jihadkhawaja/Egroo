using jihadkhawaja.chat.shared.Models;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text;

namespace Egroo.Server.Tools
{
    internal static class EgrooTool
    {
        public static readonly ISet<string> ScopedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "search_published_agents",
            "add_agent_friend",
            "add_agent_to_channel"
        };

        public static IReadOnlyList<BuiltinToolDefinition> GetDefinitions() =>
        [
            new()
            {
                Name = "search_published_agents",
                Description = "Search for published AI agents on the platform by name or description. Returns agents you own plus agents other users have allowed to be added.",
                ParametersSchema = """{"type":"object","properties":{"query":{"type":"string","description":"Search term to find agents by name or description"},"max_results":{"type":"integer","description":"Maximum number of results to return (default: 10)"}},"required":["query"]}"""
            },
            new()
            {
                Name = "add_agent_friend",
                Description = "Add a published AI agent as a friend. The agent must be published and shared by its owner for other users.",
                ParametersSchema = """{"type":"object","properties":{"agent_id":{"type":"string","description":"The unique ID (GUID) of the published agent to add as friend"}},"required":["agent_id"]}"""
            },
            new()
            {
                Name = "add_agent_to_channel",
                Description = "Add an AI agent to a channel. The caller must be an admin of the channel. The agent must be owned by the caller or be a published shared agent that the caller has added as a friend.",
                ParametersSchema = """{"type":"object","properties":{"channel_id":{"type":"string","description":"The unique ID (GUID) of the channel to add the agent to"},"agent_id":{"type":"string","description":"The unique ID (GUID) of the agent to add"}},"required":["channel_id","agent_id"]}"""
            }
        ];

        public static IList<AITool> CreateScopedTools(IServiceScopeFactory scopeFactory, Guid callerUserId)
        {
            return new List<AITool>
            {
                AIFunctionFactory.Create(
                    ([Description("Search term to find agents by name or description")] string query,
                     [Description("Maximum number of results to return (default: 10)")] int? max_results) =>
                        SearchPublishedAgentsAsync(scopeFactory, callerUserId, query, max_results ?? 10),
                    "search_published_agents",
                    "Search for published AI agents on the platform by name or description."),
                AIFunctionFactory.Create(
                    ([Description("The unique ID (GUID) of the published agent to add as friend")] string agent_id) =>
                        AddAgentFriendAsync(scopeFactory, callerUserId, agent_id),
                    "add_agent_friend",
                    "Add a published AI agent as a friend."),
                AIFunctionFactory.Create(
                    ([Description("The unique ID (GUID) of the channel")] string channel_id,
                     [Description("The unique ID (GUID) of the agent")] string agent_id) =>
                        AddAgentToChannelAsync(scopeFactory, callerUserId, channel_id, agent_id),
                    "add_agent_to_channel",
                    "Add an AI agent to a channel.")
            };
        }

        private static async Task<string> SearchPublishedAgentsAsync(IServiceScopeFactory scopeFactory, Guid callerUserId, string query, int maxResults)
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Database.DataContext>();
            var lowerQuery = query.ToLower();

            var agents = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                dbContext.AgentDefinitions
                    .Where(x => x.IsPublished && x.IsActive && x.DateDeleted == null
                        && (x.UserId == callerUserId || x.AddPermission == AgentAddPermission.OwnerAndOthers)
                        && (x.Name.ToLower().Contains(lowerQuery)
                            || (x.Description != null && x.Description.ToLower().Contains(lowerQuery))))
                    .OrderByDescending(x => x.DateCreated)
                    .Take(maxResults));

            if (agents.Count == 0)
            {
                return $"No published agents found matching '{query}'.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {agents.Count} published agent(s):");
            foreach (var agent in agents)
            {
                sb.AppendLine($"  - {agent.Name} (ID: {agent.Id})");
                if (!string.IsNullOrWhiteSpace(agent.Description))
                {
                    sb.AppendLine($"    Description: {agent.Description}");
                }

                sb.AppendLine($"    Provider: {agent.Provider}, Model: {agent.Model}");
                sb.AppendLine($"    Add permission: {(agent.AddPermission == AgentAddPermission.OwnerOnly ? "owner only" : "owner and other users")}");
            }

            return sb.ToString();
        }

        private static async Task<string> AddAgentFriendAsync(IServiceScopeFactory scopeFactory, Guid callerUserId, string agentIdStr)
        {
            if (!Guid.TryParse(agentIdStr, out var agentId))
            {
                return "Invalid agent ID format. Please provide a valid GUID.";
            }

            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Database.DataContext>();

            var agent = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                dbContext.AgentDefinitions,
                x => x.Id == agentId && x.IsPublished && x.IsActive && x.DateDeleted == null);

            if (agent is null)
            {
                return "Agent not found or is not published.";
            }

            if (agent.UserId != callerUserId && agent.AddPermission == AgentAddPermission.OwnerOnly)
            {
                return "This agent can only be added by its owner.";
            }

            var existing = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                dbContext.UserAgentFriends,
                x => x.UserId == callerUserId && x.AgentDefinitionId == agentId && x.DateDeleted == null);

            if (existing is not null)
            {
                return $"Already friends with agent '{agent.Name}'.";
            }

            await dbContext.UserAgentFriends.AddAsync(new UserAgentFriend
            {
                Id = Guid.NewGuid(),
                UserId = callerUserId,
                AgentDefinitionId = agentId,
                DateCreated = DateTimeOffset.UtcNow,
                CreatedBy = callerUserId
            });

            await dbContext.SaveChangesAsync();
            return $"Successfully added agent '{agent.Name}' as a friend.";
        }

        private static async Task<string> AddAgentToChannelAsync(IServiceScopeFactory scopeFactory, Guid callerUserId, string channelIdStr, string agentIdStr)
        {
            if (!TryParseChannelAgentIds(channelIdStr, agentIdStr, out var channelId, out var agentId))
            {
                return "Invalid ID format. Please provide valid GUIDs.";
            }

            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Database.DataContext>();

            if (!await IsChannelAdminAsync(dbContext, channelId, callerUserId))
            {
                return "You must be a channel admin to add agents.";
            }

            var agent = await GetActiveAgentAsync(dbContext, agentId);
            if (agent is null)
            {
                return "Agent not found or is not active.";
            }

            var accessError = await ValidateAgentChannelAccessAsync(dbContext, callerUserId, agent, agentId);
            if (accessError is not null)
            {
                return accessError;
            }

            if (await IsAgentAlreadyInChannelAsync(dbContext, channelId, agentId))
            {
                return $"Agent '{agent.Name}' is already in the channel.";
            }

            await dbContext.ChannelAgents.AddAsync(new ChannelAgent
            {
                Id = Guid.NewGuid(),
                ChannelId = channelId,
                AgentDefinitionId = agentId,
                AddedByUserId = callerUserId,
                DateCreated = DateTimeOffset.UtcNow,
                CreatedBy = callerUserId
            });

            await dbContext.SaveChangesAsync();
            return $"Successfully added agent '{agent.Name}' to the channel.";
        }

        internal static async Task<AgentDefinition?> GetAccessibleFriendAgentAsync(Database.DataContext dbContext, Guid callerUserId, Guid agentId)
        {
            var agent = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                dbContext.AgentDefinitions,
                x => x.Id == agentId && x.IsActive && x.DateDeleted == null);

            if (agent is null)
            {
                return null;
            }

            if (agent.UserId == callerUserId)
            {
                return agent;
            }

            if (!agent.IsPublished || agent.AddPermission == AgentAddPermission.OwnerOnly)
            {
                return null;
            }

            return await HasFriendAccessAsync(dbContext, callerUserId, agentId) ? agent : null;
        }

        internal static async Task<IReadOnlyList<AgentDefinition>> GetFriendAgentsAsync(Database.DataContext dbContext, Guid callerUserId)
        {
            var friendAgentIds = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                dbContext.UserAgentFriends
                    .Where(x => x.UserId == callerUserId && x.DateDeleted == null)
                    .Select(x => x.AgentDefinitionId));

            if (friendAgentIds.Count == 0)
            {
                return [];
            }

            return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                dbContext.AgentDefinitions
                    .Where(x => friendAgentIds.Contains(x.Id) && x.IsActive && x.DateDeleted == null)
                    .OrderBy(x => x.Name));
        }

        private static bool TryParseChannelAgentIds(string channelIdStr, string agentIdStr, out Guid channelId, out Guid agentId)
        {
            channelId = Guid.Empty;
            agentId = Guid.Empty;
            return Guid.TryParse(channelIdStr, out channelId) && Guid.TryParse(agentIdStr, out agentId);
        }

        private static Task<bool> IsChannelAdminAsync(Database.DataContext dbContext, Guid channelId, Guid callerUserId)
        {
            return Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
                dbContext.ChannelUsers,
                x => x.ChannelId == channelId && x.UserId == callerUserId && x.IsAdmin);
        }

        private static Task<AgentDefinition?> GetActiveAgentAsync(Database.DataContext dbContext, Guid agentId)
        {
            return Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                dbContext.AgentDefinitions,
                x => x.Id == agentId && x.IsActive && x.DateDeleted == null);
        }

        private static async Task<string?> ValidateAgentChannelAccessAsync(Database.DataContext dbContext, Guid callerUserId, AgentDefinition agent, Guid agentId)
        {
            if (agent.UserId == callerUserId)
            {
                return null;
            }

            if (!agent.IsPublished)
            {
                return "Agent is not published and you are not the owner.";
            }

            if (agent.AddPermission == AgentAddPermission.OwnerOnly)
            {
                return "This agent can only be added by its owner.";
            }

            return await HasFriendAccessAsync(dbContext, callerUserId, agentId)
                ? null
                : "Add this agent as a friend before inviting it to a channel.";
        }

        private static Task<bool> HasFriendAccessAsync(Database.DataContext dbContext, Guid callerUserId, Guid agentId)
        {
            return Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
                dbContext.UserAgentFriends,
                x => x.UserId == callerUserId && x.AgentDefinitionId == agentId && x.DateDeleted == null);
        }

        private static Task<bool> IsAgentAlreadyInChannelAsync(Database.DataContext dbContext, Guid channelId, Guid agentId)
        {
            return Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
                dbContext.ChannelAgents,
                x => x.ChannelId == channelId && x.AgentDefinitionId == agentId && x.DateDeleted == null);
        }
    }
}