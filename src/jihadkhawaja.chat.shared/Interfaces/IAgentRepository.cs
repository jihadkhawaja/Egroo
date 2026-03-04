using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    /// <summary>
    /// Repository interface for AI agent CRUD operations.
    /// </summary>
    public interface IAgentRepository
    {
        // ── Agent Definition ─────────────────────────────────────────────
        Task<AgentDefinition?> CreateAgent(AgentDefinition definition);
        Task<AgentDefinition?> GetAgent(Guid agentId);
        Task<AgentDefinition[]?> GetUserAgents();
        Task<bool> UpdateAgent(AgentDefinition definition);
        Task<bool> DeleteAgent(Guid agentId);

        // ── Knowledge ────────────────────────────────────────────────────
        Task<AgentKnowledge?> AddKnowledge(AgentKnowledge knowledge);
        Task<AgentKnowledge[]?> GetAgentKnowledge(Guid agentId);
        Task<bool> UpdateKnowledge(AgentKnowledge knowledge);
        Task<bool> DeleteKnowledge(Guid knowledgeId);

        // ── Tools ────────────────────────────────────────────────────────
        Task<AgentTool?> AddTool(AgentTool tool);
        Task<AgentTool[]?> GetAgentTools(Guid agentId);
        Task<bool> UpdateTool(AgentTool tool);
        Task<bool> DeleteTool(Guid toolId);
        Task<bool> DeleteToolsByMcpServer(Guid mcpServerId);

        // ── MCP Servers ──────────────────────────────────────────────────
        Task<AgentMcpServer?> AddMcpServer(AgentMcpServer server);
        Task<AgentMcpServer[]?> GetAgentMcpServers(Guid agentId);
        Task<AgentMcpServer?> GetMcpServer(Guid serverId);
        Task<bool> UpdateMcpServer(AgentMcpServer server);
        Task<bool> DeleteMcpServer(Guid serverId);

        // ── Conversations ────────────────────────────────────────────────
        Task<AgentConversation?> CreateConversation(Guid agentId, string? title = null);
        Task<AgentConversation?> GetConversation(Guid conversationId);
        Task<AgentConversation[]?> GetUserConversations(Guid agentId);
        Task<bool> UpdateConversationSessionState(Guid conversationId, string? sessionState);
        Task<bool> DeleteConversation(Guid conversationId);

        // ── Messages ─────────────────────────────────────────────────────
        Task<AgentConversationMessage?> AddMessage(AgentConversationMessage message);
        Task<AgentConversationMessage[]?> GetConversationMessages(Guid conversationId, int skip = 0, int take = 50);
    }
}
