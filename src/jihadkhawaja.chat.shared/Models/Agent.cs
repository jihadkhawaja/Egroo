using System.ComponentModel.DataAnnotations;

namespace jihadkhawaja.chat.shared.Models
{
    /// <summary>
    /// Supported LLM provider types for agent configuration.
    /// </summary>
    public enum LlmProvider
    {
        OpenAI = 0,
        AzureOpenAI = 1,
        Anthropic = 2,
        Ollama = 3
    }

    /// <summary>
    /// Controls who is allowed to add a published agent.
    /// </summary>
    public enum AgentAddPermission
    {
        OwnerOnly = 0,
        OwnerAndOthers = 1
    }

    /// <summary>
    /// Represents a user-created AI agent definition with its LLM configuration,
    /// instructions, knowledge, and tools.
    /// </summary>
    public class AgentDefinition : EntityBase
    {
        /// <summary>
        /// The user who owns this agent.
        /// </summary>
        [Required]
        public Guid UserId { get; set; }

        /// <summary>
        /// Display name of the agent.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of what the agent does.
        /// </summary>
        [MaxLength(2000)]
        public string? Description { get; set; }

        /// <summary>
        /// System instructions / persona for the agent.
        /// </summary>
        public string? Instructions { get; set; }

        /// <summary>
        /// The LLM provider to use (OpenAI, AzureOpenAI, Anthropic, Ollama).
        /// </summary>
        [Required]
        public LlmProvider Provider { get; set; }

        /// <summary>
        /// The model identifier (e.g. "gpt-4o-mini", "claude-haiku-4-5", "llama3.2").
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Encrypted API key / token for the LLM provider. Stored encrypted at rest.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Optional endpoint URL (required for AzureOpenAI and Ollama).
        /// </summary>
        [MaxLength(500)]
        public string? Endpoint { get; set; }

        /// <summary>
        /// Whether this agent is currently active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Whether this agent is published and discoverable by other users.
        /// Published agents can be added as friends and invited to channels.
        /// </summary>
        public bool IsPublished { get; set; }

        /// <summary>
        /// Controls who can add this published agent.
        /// </summary>
        public AgentAddPermission AddPermission { get; set; } = AgentAddPermission.OwnerAndOthers;

        /// <summary>
        /// Temperature setting for LLM responses (0.0 - 1.0).
        /// </summary>
        public float? Temperature { get; set; }

        /// <summary>
        /// Max tokens for LLM responses.
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// Optional custom skills advertisement prompt template. Must contain a {0} placeholder.
        /// </summary>
        [MaxLength(4000)]
        public string? SkillsInstructionPrompt { get; set; }
    }

    /// <summary>
    /// Represents a filesystem directory or skill folder made available to an agent via Agent Skills.
    /// </summary>
    public class AgentSkillDirectory : EntityBase
    {
        [Required]
        public Guid AgentDefinitionId { get; set; }

        /// <summary>
        /// Friendly name shown to users when selecting or reviewing skills.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Absolute or server-relative path to a skill folder or a parent folder containing skills.
        /// </summary>
        [Required]
        [MaxLength(2000)]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Whether this skill directory should be active for agent runs.
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// A knowledge item attached to an agent. This is injected as context
    /// into the agent's system prompt or conversation.
    /// </summary>
    public class AgentKnowledge : EntityBase
    {
        [Required]
        public Guid AgentDefinitionId { get; set; }

        /// <summary>
        /// Title/label for this knowledge item.
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// The knowledge content (text, markdown, etc.).
        /// </summary>
        [Required]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Whether this knowledge item is currently enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// Source type indicating where a tool originates from.
    /// </summary>
    public enum AgentToolSource
    {
        /// <summary>Built-in tool provided by the platform.</summary>
        Builtin = 0,
        /// <summary>Tool discovered from an MCP server connection.</summary>
        Mcp = 1
    }

    /// <summary>
    /// Represents a connection to an MCP (Model Context Protocol) server
    /// that provides tools to an agent.
    /// </summary>
    public class AgentMcpServer : EntityBase
    {
        [Required]
        public Guid AgentDefinitionId { get; set; }

        /// <summary>
        /// Display name for the MCP server connection.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The MCP server endpoint URL (SSE or Streamable HTTP transport).
        /// </summary>
        [Required]
        [MaxLength(1000)]
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Optional API key or bearer token for authenticating with the MCP server.
        /// Stored encrypted at rest.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Whether this MCP server connection is active and its tools should be loaded.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Timestamp of the last successful tool discovery from this server.
        /// </summary>
        public DateTimeOffset? LastDiscoveredAt { get; set; }
    }

    /// <summary>
    /// A tool definition that can be attached to an agent.
    /// Tools are defined as named function descriptions that the LLM can invoke.
    /// </summary>
    public class AgentTool : EntityBase
    {
        [Required]
        public Guid AgentDefinitionId { get; set; }

        /// <summary>
        /// The tool function name (must be a valid identifier).
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of what the tool does (shown to the LLM).
        /// </summary>
        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// JSON schema defining the tool's parameters.
        /// Example: {"type":"object","properties":{"location":{"type":"string","description":"City name"}}}
        /// </summary>
        public string? ParametersSchema { get; set; }

        /// <summary>
        /// Whether this tool is currently enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// The source of this tool (Builtin or Mcp).
        /// </summary>
        public AgentToolSource Source { get; set; } = AgentToolSource.Builtin;

        /// <summary>
        /// If Source is Mcp, the ID of the MCP server this tool was discovered from.
        /// </summary>
        public Guid? McpServerId { get; set; }
    }

    /// <summary>
    /// Represents a conversation session with an agent.
    /// </summary>
    public class AgentConversation : EntityBase
    {
        [Required]
        public Guid AgentDefinitionId { get; set; }

        /// <summary>
        /// The user who started this conversation.
        /// </summary>
        [Required]
        public Guid UserId { get; set; }

        /// <summary>
        /// Optional title for the conversation.
        /// </summary>
        [MaxLength(500)]
        public string? Title { get; set; }

        /// <summary>
        /// Serialized session state from the Agent Framework (for resuming sessions).
        /// </summary>
        public string? SessionState { get; set; }
    }

    /// <summary>
    /// A single message in an agent conversation.
    /// </summary>
    public class AgentConversationMessage : EntityBase
    {
        [Required]
        public Guid AgentConversationId { get; set; }

        /// <summary>
        /// The role of the message sender ("user", "assistant", "system", "tool").
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// The message content.
        /// </summary>
        [Required]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Optional tool call ID if this is a tool response.
        /// </summary>
        [MaxLength(200)]
        public string? ToolCallId { get; set; }

        /// <summary>
        /// Optional tool name if this is a tool call/response.
        /// </summary>
        [MaxLength(200)]
        public string? ToolName { get; set; }

        /// <summary>
        /// Token usage for this message (if available from LLM response).
        /// </summary>
        public int? TokensUsed { get; set; }
    }
}
