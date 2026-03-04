using Egroo.Server.Database;
using Egroo.Server.Security;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Egroo.Server.Repository
{
    public class AgentRepository : BaseRepository, IAgentRepository
    {
        private readonly EncryptionService _encryptionService;

        public AgentRepository(DataContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            IConnectionTracker connectionTracker,
            EncryptionService encryptionService,
            ILogger<AgentRepository> logger)
            : base(dbContext, httpContextAccessor, configuration, connectionTracker, logger)
        {
            _encryptionService = encryptionService;
        }

        // ── Agent Definition ─────────────────────────────────────────────

        public async Task<AgentDefinition?> CreateAgent(AgentDefinition definition)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            definition.Id = Guid.NewGuid();
            definition.UserId = userId.Value;
            definition.DateCreated = DateTimeOffset.UtcNow;

            // Encrypt the API key before storing
            if (!string.IsNullOrWhiteSpace(definition.ApiKey))
            {
                definition.ApiKey = _encryptionService.Encrypt(definition.ApiKey);
            }

            try
            {
                await _dbContext.AgentDefinitions.AddAsync(definition);
                await _dbContext.SaveChangesAsync();
                return definition;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create agent definition");
                return null;
            }
        }

        public async Task<AgentDefinition?> GetAgent(Guid agentId)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            return await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == agentId && x.UserId == userId.Value && x.DateDeleted == null);
        }

        public async Task<AgentDefinition[]?> GetUserAgents()
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            return await _dbContext.AgentDefinitions
                .Where(x => x.UserId == userId.Value && x.DateDeleted == null)
                .OrderByDescending(x => x.DateCreated)
                .ToArrayAsync();
        }

        public async Task<bool> UpdateAgent(AgentDefinition definition)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return false;
            }

            var existing = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == definition.Id && x.UserId == userId.Value && x.DateDeleted == null);

            if (existing is null)
            {
                return false;
            }

            existing.Name = definition.Name;
            existing.Description = definition.Description;
            existing.Instructions = definition.Instructions;
            existing.Provider = definition.Provider;
            existing.Model = definition.Model;
            existing.Endpoint = definition.Endpoint;
            existing.IsActive = definition.IsActive;
            existing.Temperature = definition.Temperature;
            existing.MaxTokens = definition.MaxTokens;
            existing.DateUpdated = DateTimeOffset.UtcNow;
            existing.UpdatedBy = userId.Value;

            // Only update API key if a new value was provided
            if (!string.IsNullOrWhiteSpace(definition.ApiKey))
            {
                existing.ApiKey = _encryptionService.Encrypt(definition.ApiKey);
            }

            try
            {
                _dbContext.AgentDefinitions.Update(existing);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update agent definition");
                return false;
            }
        }

        public async Task<bool> DeleteAgent(Guid agentId)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return false;
            }

            var existing = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == agentId && x.UserId == userId.Value && x.DateDeleted == null);

            if (existing is null)
            {
                return false;
            }

            existing.DateDeleted = DateTimeOffset.UtcNow;
            existing.DeletedBy = userId.Value;

            try
            {
                _dbContext.AgentDefinitions.Update(existing);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete agent definition");
                return false;
            }
        }

        // ── Knowledge ────────────────────────────────────────────────────

        public async Task<AgentKnowledge?> AddKnowledge(AgentKnowledge knowledge)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            // Verify agent ownership
            var agent = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == knowledge.AgentDefinitionId && x.UserId == userId.Value && x.DateDeleted == null);

            if (agent is null)
            {
                return null;
            }

            knowledge.Id = Guid.NewGuid();
            knowledge.DateCreated = DateTimeOffset.UtcNow;
            knowledge.CreatedBy = userId.Value;

            try
            {
                await _dbContext.AgentKnowledgeItems.AddAsync(knowledge);
                await _dbContext.SaveChangesAsync();
                return knowledge;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add agent knowledge");
                return null;
            }
        }

        public async Task<AgentKnowledge[]?> GetAgentKnowledge(Guid agentId)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            // Verify agent ownership
            var agent = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == agentId && x.UserId == userId.Value && x.DateDeleted == null);

            if (agent is null)
            {
                return null;
            }

            return await _dbContext.AgentKnowledgeItems
                .Where(x => x.AgentDefinitionId == agentId && x.DateDeleted == null)
                .OrderBy(x => x.DateCreated)
                .ToArrayAsync();
        }

        public async Task<bool> UpdateKnowledge(AgentKnowledge knowledge)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return false;
            }

            var existing = await _dbContext.AgentKnowledgeItems
                .FirstOrDefaultAsync(x => x.Id == knowledge.Id && x.DateDeleted == null);

            if (existing is null)
            {
                return false;
            }

            // Verify agent ownership
            var agent = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == existing.AgentDefinitionId && x.UserId == userId.Value && x.DateDeleted == null);

            if (agent is null)
            {
                return false;
            }

            existing.Title = knowledge.Title;
            existing.Content = knowledge.Content;
            existing.IsEnabled = knowledge.IsEnabled;
            existing.DateUpdated = DateTimeOffset.UtcNow;
            existing.UpdatedBy = userId.Value;

            try
            {
                _dbContext.AgentKnowledgeItems.Update(existing);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update agent knowledge");
                return false;
            }
        }

        public async Task<bool> DeleteKnowledge(Guid knowledgeId)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return false;
            }

            var existing = await _dbContext.AgentKnowledgeItems
                .FirstOrDefaultAsync(x => x.Id == knowledgeId && x.DateDeleted == null);

            if (existing is null)
            {
                return false;
            }

            // Verify agent ownership
            var agent = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == existing.AgentDefinitionId && x.UserId == userId.Value && x.DateDeleted == null);

            if (agent is null)
            {
                return false;
            }

            existing.DateDeleted = DateTimeOffset.UtcNow;
            existing.DeletedBy = userId.Value;

            try
            {
                _dbContext.AgentKnowledgeItems.Update(existing);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete agent knowledge");
                return false;
            }
        }

        // ── Tools ────────────────────────────────────────────────────────

        public async Task<AgentTool?> AddTool(AgentTool tool)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            // Verify agent ownership
            var agent = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == tool.AgentDefinitionId && x.UserId == userId.Value && x.DateDeleted == null);

            if (agent is null)
            {
                return null;
            }

            tool.Id = Guid.NewGuid();
            tool.DateCreated = DateTimeOffset.UtcNow;
            tool.CreatedBy = userId.Value;

            try
            {
                await _dbContext.AgentTools.AddAsync(tool);
                await _dbContext.SaveChangesAsync();
                return tool;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add agent tool");
                return null;
            }
        }

        public async Task<AgentTool[]?> GetAgentTools(Guid agentId)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            // Verify agent ownership
            var agent = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == agentId && x.UserId == userId.Value && x.DateDeleted == null);

            if (agent is null)
            {
                return null;
            }

            return await _dbContext.AgentTools
                .Where(x => x.AgentDefinitionId == agentId && x.DateDeleted == null)
                .OrderBy(x => x.DateCreated)
                .ToArrayAsync();
        }

        public async Task<bool> UpdateTool(AgentTool tool)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return false;
            }

            var existing = await _dbContext.AgentTools
                .FirstOrDefaultAsync(x => x.Id == tool.Id && x.DateDeleted == null);

            if (existing is null)
            {
                return false;
            }

            // Verify agent ownership
            var agent = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == existing.AgentDefinitionId && x.UserId == userId.Value && x.DateDeleted == null);

            if (agent is null)
            {
                return false;
            }

            existing.Name = tool.Name;
            existing.Description = tool.Description;
            existing.ParametersSchema = tool.ParametersSchema;
            existing.IsEnabled = tool.IsEnabled;
            existing.DateUpdated = DateTimeOffset.UtcNow;
            existing.UpdatedBy = userId.Value;

            try
            {
                _dbContext.AgentTools.Update(existing);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update agent tool");
                return false;
            }
        }

        public async Task<bool> DeleteTool(Guid toolId)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return false;
            }

            var existing = await _dbContext.AgentTools
                .FirstOrDefaultAsync(x => x.Id == toolId && x.DateDeleted == null);

            if (existing is null)
            {
                return false;
            }

            // Verify agent ownership
            var agent = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == existing.AgentDefinitionId && x.UserId == userId.Value && x.DateDeleted == null);

            if (agent is null)
            {
                return false;
            }

            existing.DateDeleted = DateTimeOffset.UtcNow;
            existing.DeletedBy = userId.Value;

            try
            {
                _dbContext.AgentTools.Update(existing);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete agent tool");
                return false;
            }
        }

        public async Task<bool> DeleteToolsByMcpServer(Guid mcpServerId)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return false;
            }

            var tools = await _dbContext.AgentTools
                .Where(x => x.McpServerId == mcpServerId && x.DateDeleted == null)
                .ToListAsync();

            foreach (var tool in tools)
            {
                tool.DateDeleted = DateTimeOffset.UtcNow;
                tool.DeletedBy = userId.Value;
            }

            try
            {
                _dbContext.AgentTools.UpdateRange(tools);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete tools for MCP server {McpServerId}", mcpServerId);
                return false;
            }
        }

        // ── MCP Servers ──────────────────────────────────────────────────

        public async Task<AgentMcpServer?> AddMcpServer(AgentMcpServer server)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            // Verify agent ownership
            var agent = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == server.AgentDefinitionId && x.UserId == userId.Value && x.DateDeleted == null);

            if (agent is null)
            {
                return null;
            }

            server.Id = Guid.NewGuid();
            server.DateCreated = DateTimeOffset.UtcNow;
            server.CreatedBy = userId.Value;

            // Encrypt API key if provided
            if (!string.IsNullOrWhiteSpace(server.ApiKey))
            {
                server.ApiKey = _encryptionService.Encrypt(server.ApiKey);
            }

            try
            {
                await _dbContext.AgentMcpServers.AddAsync(server);
                await _dbContext.SaveChangesAsync();
                return server;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add MCP server");
                return null;
            }
        }

        public async Task<AgentMcpServer[]?> GetAgentMcpServers(Guid agentId)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            var agent = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == agentId && x.UserId == userId.Value && x.DateDeleted == null);

            if (agent is null)
            {
                return null;
            }

            return await _dbContext.AgentMcpServers
                .Where(x => x.AgentDefinitionId == agentId && x.DateDeleted == null)
                .OrderBy(x => x.DateCreated)
                .ToArrayAsync();
        }

        public async Task<AgentMcpServer?> GetMcpServer(Guid serverId)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            var server = await _dbContext.AgentMcpServers
                .FirstOrDefaultAsync(x => x.Id == serverId && x.DateDeleted == null);

            if (server is null)
            {
                return null;
            }

            // Verify agent ownership
            var agent = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == server.AgentDefinitionId && x.UserId == userId.Value && x.DateDeleted == null);

            if (agent is null)
            {
                return null;
            }

            return server;
        }

        public async Task<bool> UpdateMcpServer(AgentMcpServer server)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return false;
            }

            var existing = await _dbContext.AgentMcpServers
                .FirstOrDefaultAsync(x => x.Id == server.Id && x.DateDeleted == null);

            if (existing is null)
            {
                return false;
            }

            var agent = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == existing.AgentDefinitionId && x.UserId == userId.Value && x.DateDeleted == null);

            if (agent is null)
            {
                return false;
            }

            existing.Name = server.Name;
            existing.Endpoint = server.Endpoint;
            existing.IsActive = server.IsActive;
            existing.DateUpdated = DateTimeOffset.UtcNow;
            existing.UpdatedBy = userId.Value;

            // Update API key only if a new one is provided
            if (!string.IsNullOrWhiteSpace(server.ApiKey))
            {
                existing.ApiKey = _encryptionService.Encrypt(server.ApiKey);
            }

            if (server.LastDiscoveredAt.HasValue)
            {
                existing.LastDiscoveredAt = server.LastDiscoveredAt;
            }

            try
            {
                _dbContext.AgentMcpServers.Update(existing);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update MCP server");
                return false;
            }
        }

        public async Task<bool> DeleteMcpServer(Guid serverId)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return false;
            }

            var existing = await _dbContext.AgentMcpServers
                .FirstOrDefaultAsync(x => x.Id == serverId && x.DateDeleted == null);

            if (existing is null)
            {
                return false;
            }

            var agent = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == existing.AgentDefinitionId && x.UserId == userId.Value && x.DateDeleted == null);

            if (agent is null)
            {
                return false;
            }

            existing.DateDeleted = DateTimeOffset.UtcNow;
            existing.DeletedBy = userId.Value;

            try
            {
                // Also soft-delete all tools from this MCP server
                var mcpTools = await _dbContext.AgentTools
                    .Where(x => x.McpServerId == serverId && x.DateDeleted == null)
                    .ToListAsync();

                foreach (var tool in mcpTools)
                {
                    tool.DateDeleted = DateTimeOffset.UtcNow;
                    tool.DeletedBy = userId.Value;
                }

                _dbContext.AgentMcpServers.Update(existing);
                _dbContext.AgentTools.UpdateRange(mcpTools);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete MCP server");
                return false;
            }
        }

        // ── Conversations ────────────────────────────────────────────────

        public async Task<AgentConversation?> CreateConversation(Guid agentId, string? title = null)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            // Verify agent ownership
            var agent = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == agentId && x.UserId == userId.Value && x.DateDeleted == null);

            if (agent is null)
            {
                return null;
            }

            var conversation = new AgentConversation
            {
                Id = Guid.NewGuid(),
                AgentDefinitionId = agentId,
                UserId = userId.Value,
                Title = title,
                DateCreated = DateTimeOffset.UtcNow,
                CreatedBy = userId.Value
            };

            try
            {
                await _dbContext.AgentConversations.AddAsync(conversation);
                await _dbContext.SaveChangesAsync();
                return conversation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create agent conversation");
                return null;
            }
        }

        public async Task<AgentConversation?> GetConversation(Guid conversationId)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            return await _dbContext.AgentConversations
                .FirstOrDefaultAsync(x => x.Id == conversationId && x.UserId == userId.Value && x.DateDeleted == null);
        }

        public async Task<AgentConversation[]?> GetUserConversations(Guid agentId)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            return await _dbContext.AgentConversations
                .Where(x => x.AgentDefinitionId == agentId && x.UserId == userId.Value && x.DateDeleted == null)
                .OrderByDescending(x => x.DateCreated)
                .ToArrayAsync();
        }

        public async Task<bool> UpdateConversationSessionState(Guid conversationId, string? sessionState)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return false;
            }

            var existing = await _dbContext.AgentConversations
                .FirstOrDefaultAsync(x => x.Id == conversationId && x.UserId == userId.Value && x.DateDeleted == null);

            if (existing is null)
            {
                return false;
            }

            existing.SessionState = sessionState;
            existing.DateUpdated = DateTimeOffset.UtcNow;

            try
            {
                _dbContext.AgentConversations.Update(existing);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update conversation session state");
                return false;
            }
        }

        public async Task<bool> DeleteConversation(Guid conversationId)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return false;
            }

            var existing = await _dbContext.AgentConversations
                .FirstOrDefaultAsync(x => x.Id == conversationId && x.UserId == userId.Value && x.DateDeleted == null);

            if (existing is null)
            {
                return false;
            }

            existing.DateDeleted = DateTimeOffset.UtcNow;
            existing.DeletedBy = userId.Value;

            try
            {
                _dbContext.AgentConversations.Update(existing);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete agent conversation");
                return false;
            }
        }

        // ── Messages ─────────────────────────────────────────────────────

        public async Task<AgentConversationMessage?> AddMessage(AgentConversationMessage message)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            // Verify conversation ownership
            var conversation = await _dbContext.AgentConversations
                .FirstOrDefaultAsync(x => x.Id == message.AgentConversationId && x.UserId == userId.Value && x.DateDeleted == null);

            if (conversation is null)
            {
                return null;
            }

            message.Id = Guid.NewGuid();
            message.DateCreated = DateTimeOffset.UtcNow;
            message.CreatedBy = userId.Value;

            try
            {
                await _dbContext.AgentConversationMessages.AddAsync(message);
                await _dbContext.SaveChangesAsync();
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add agent conversation message");
                return null;
            }
        }

        public async Task<AgentConversationMessage[]?> GetConversationMessages(Guid conversationId, int skip = 0, int take = 50)
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            // Verify conversation ownership
            var conversation = await _dbContext.AgentConversations
                .FirstOrDefaultAsync(x => x.Id == conversationId && x.UserId == userId.Value && x.DateDeleted == null);

            if (conversation is null)
            {
                return null;
            }

            return await _dbContext.AgentConversationMessages
                .Where(x => x.AgentConversationId == conversationId && x.DateDeleted == null)
                .OrderBy(x => x.DateCreated)
                .Skip(skip)
                .Take(take)
                .ToArrayAsync();
        }

        // ── Internal helpers (used by AgentRuntimeService) ───────────────

        /// <summary>
        /// Get agent with decrypted API key (internal use only).
        /// </summary>
        internal async Task<(AgentDefinition agent, string? decryptedApiKey)?> GetAgentWithDecryptedKey(Guid agentId, Guid userId)
        {
            var agent = await _dbContext.AgentDefinitions
                .FirstOrDefaultAsync(x => x.Id == agentId && x.UserId == userId && x.DateDeleted == null);

            if (agent is null)
            {
                return null;
            }

            string? decryptedKey = null;
            if (!string.IsNullOrWhiteSpace(agent.ApiKey))
            {
                decryptedKey = _encryptionService.Decrypt(agent.ApiKey);
            }

            return (agent, decryptedKey);
        }

        /// <summary>
        /// Add a message without checking ownership (used internally by the runtime).
        /// </summary>
        internal async Task<AgentConversationMessage?> AddMessageInternal(AgentConversationMessage message)
        {
            message.Id = Guid.NewGuid();
            message.DateCreated = DateTimeOffset.UtcNow;

            try
            {
                await _dbContext.AgentConversationMessages.AddAsync(message);
                await _dbContext.SaveChangesAsync();
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add agent conversation message (internal)");
                return null;
            }
        }
    }
}
