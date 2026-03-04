---
name: agent-framework
description: 'Build, extend, and debug AI agents in Egroo using the Microsoft Agent Framework (C# .NET). Use for: creating AgentDefinition, adding function tools with AIFunctionFactory, connecting MCP servers, managing agent conversations and session state, wiring new LLM providers (OpenAI, AzureOpenAI, Anthropic, Ollama), adding knowledge items, implementing streaming chat. Also covers AgentRuntimeService internals, BuiltinTools, and McpClientService patterns already in the codebase.'
argument-hint: 'Describe what you want to build or fix (e.g. "add a weather tool", "new Ollama agent", "debug streaming")'
---

# Microsoft Agent Framework — Egroo Integration

## When to Use

- Creating or modifying an `AgentDefinition` and its associated CRUD endpoints
- Adding a new **function tool** (builtin or MCP-backed)
- Wiring a new **LLM provider** (OpenAI, Azure OpenAI, Anthropic, Ollama)
- Working with **agent conversations**, session state, or message history
- Implementing or debugging **streaming chat** (`ChatStreamAsync`)
- Adding **knowledge items** injected into the system prompt
- Integrating a new **MCP server** for tool discovery/invocation

---

## Key Files

| File | Purpose |
|---|---|
| `src/Egroo.Server/Services/AgentRuntimeService.cs` | Core: builds `AIAgent` from `AgentDefinition`, runs chat turns, manages history |
| `src/Egroo.Server/Services/BuiltinTools.cs` | Predefined `AIFunction` tools (datetime, timezone) — add new builtins here |
| `src/Egroo.Server/Services/McpClientService.cs` | JSON-RPC HTTP client for MCP tool discovery and invocation |
| `src/Egroo.Server/API/AgentEndpoint.cs` | All `/api/v1/Agent` Minimal API routes |
| `src/jihadkhawaja.chat.shared/Models/Agent.cs` | `AgentDefinition`, `AgentTool`, `AgentKnowledge`, `AgentConversation`, `AgentConversationMessage`, `LlmProvider` enum |
| `src/jihadkhawaja.chat.shared/Interfaces/IAgentRepository.cs` | Repository interface for all agent CRUD operations |
| `src/Egroo.Server/Repository/AgentRepository.cs` | EF Core implementation of `IAgentRepository` |
| `src/jihadkhawaja.chat.client/Services/AgentService.cs` | Client-side HTTP service (mirrors server endpoints) |

For framework details, see [./references/ms-agent-framework.md](./references/ms-agent-framework.md).

---

## Architecture

```
Client (Blazor)
  └─ AgentService (HTTP)
       └─ POST /api/v1/Agent/conversations/{id}/send-message
            └─ AgentRuntimeService.ChatAsync / ChatStreamAsync
                 ├─ IAgentRepository  → loads AgentDefinition, knowledge, tools, history
                 ├─ EncryptionService → decrypts agent API key
                 ├─ AIAgent (Microsoft.Agents.AI)
                 │    └─ provider: OpenAI | AzureOpenAI | Anthropic | Ollama
                 ├─ BuiltinTools     → pre-built AIFunctions
                 └─ McpClientService → dynamic MCP tool proxies
```

---

## Procedure

### 1. Creating a New Agent (Backend)

1. **Model**: `AgentDefinition` (in `jihadkhawaja.chat.shared/Models/Agent.cs`) holds all config. Required fields: `UserId`, `Name`, `Provider` (`LlmProvider` enum), `Model`, `ApiKey` (stored AES-encrypted), `Instructions`.

2. **Encrypt the API key** before saving — `EncryptionService.Encrypt(apiKey)`. Keys are stripped from all API responses.

3. **Register** via `POST /api/v1/Agent` — already wired in `AgentEndpoint.cs`.

4. **Repository**: All CRUD goes through `IAgentRepository` → `AgentRepository`. Register as **scoped**.

### 2. Adding a Builtin Function Tool

Add to `src/Egroo.Server/Services/BuiltinTools.cs`:

```csharp
public static AIFunction MyNewTool { get; } = AIFunctionFactory.Create(
    ([Description("The input")] string input) =>
    {
        // ... logic ...
        return Task.FromResult("result");
    },
    name: "my_new_tool",
    description: "What this tool does.");
```

Then include it in `AgentRuntimeService` alongside existing builtins:

```csharp
var tools = new List<AIFunction> { BuiltinTools.GetCurrentDatetime, BuiltinTools.MyNewTool };
```

Seed it for an agent via `POST /api/v1/Agent/{agentId}/seed-builtin-tools`.

### 3. Wiring a New LLM Provider

`AgentRuntimeService` switches on `AgentDefinition.Provider`. Add a new case:

```csharp
LlmProvider.Ollama => new OllamaApiClient(new Uri(definition.Endpoint!))
    .AsChatClient(definition.Model)
    .AsAIAgent(instructions: systemPrompt, tools: tools),
```

Available NuGet packages already in `Egroo.Server.csproj`:
- `Microsoft.Agents.AI.OpenAI` — `.GetChatClient(model).AsAIAgent(...)`
- `Microsoft.Agents.AI.Anthropic` — `AnthropicClient.AsAIAgent(...)`
- `Microsoft.Extensions.AI.Ollama` — `OllamaApiClient.AsChatClient(model).AsAIAgent(...)`
- `Azure.AI.OpenAI` — `AzureOpenAIClient.GetChatClient(model).AsAIAgent(...)`

### 4. Adding Knowledge Items

Knowledge is injected into the system prompt by `AgentRuntimeService`:

```csharp
var knowledgeItems = await _agentRepo.GetAgentKnowledgeAsync(agentId);
var systemPrompt = definition.Instructions + "\n\n" +
    string.Join("\n", knowledgeItems.Where(k => k.IsEnabled).Select(k => k.Content));
```

Add via `POST /api/v1/Agent/{agentId}/knowledge`.

### 5. Connecting an MCP Server

1. Register: `POST /api/v1/Agent/{agentId}/mcp-servers` with endpoint + optional API key.
2. Discover tools: `POST /api/v1/Agent/{agentId}/mcp-servers/discover-tools` — calls `McpClientService.DiscoverToolsAsync` and persists to `AgentTool` table with `Source = Mcp`.
3. At runtime, `AgentRuntimeService` wraps each MCP tool as an `AIFunction` that proxies through `McpClientService.CallToolAsync`.

`McpClientService` uses standard JSON-RPC 2.0 (`tools/list`, `tools/call`). API keys are sent as `Authorization: Bearer` headers.

### 6. Running a Conversation Turn

```csharp
// Non-streaming
var response = await _agentRuntimeService.ChatAsync(userId, conversationId, "Hello");

// Streaming (IAsyncEnumerable<string>)
await foreach (var token in _agentRuntimeService.ChatStreamAsync(userId, conversationId, "Hello"))
{
    // push token to client via SignalR or SSE
}
```

Session state and message history are persisted per `AgentConversation.Id` via `IAgentRepository`.

### 7. Adding a New API Endpoint

Follow the Minimal API group pattern in `AgentEndpoint.cs`:

```csharp
group.MapPost("/{agentId}/my-feature", async (IAgentRepository repo, Guid agentId, MyRequest req, HttpContext ctx) =>
{
    // use repo, return Results.Ok/NotFound
})
.RequireAuthorization();
```

All agent routes require JWT auth (`RequireAuthorization()`) and inherit the `"Api"` rate limit policy.

---

## Security Checklist

- [ ] API keys are **always encrypted** with `EncryptionService` before DB storage
- [ ] API keys are **stripped from all responses** (check `AgentEndpoint.cs` response mapping)
- [ ] MCP server endpoints should be validated/allowlisted before calling in production
- [ ] `GetConnectorUserId()` from `BaseRepository` is used to scope agents to the authenticated user
- [ ] Never log decrypted API keys

---

## Common Pitfalls

- **`LlmProvider.Ollama` requires an `Endpoint`** field on `AgentDefinition` — validate it non-null before calling.
- **Conversation `SessionState`** is stored as serialized JSON in the DB — full history is loaded on every turn. For long conversations, consider truncation.
- **MCP tool discover is manual** — tools are not auto-refreshed; call the discover endpoint after updating an MCP server.
- **`jihadkhawaja.chat.shared` changes** require rebuilding shared → server/client in order before tests pass.
- **Streaming endpoint** must be called with `HttpCompletionOption.ResponseHeadersRead` from the client.
