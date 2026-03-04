# Microsoft Agent Framework — Reference

Docs: https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp

## Core Abstractions

| Concept | C# Type | Purpose |
|---|---|---|
| Agent | `AIAgent` | Base for all agent types; wraps `IChatClient` |
| Chat agent | `ChatClientAgent` | Agent over any `IChatClient` |
| Function tool | `AIFunction` | Custom C# code callable by the LLM |
| Tool factory | `AIFunctionFactory.Create(...)` | Reflection-based `AIFunction` from a delegate/method |
| Agent as tool | `agent.AsAIFunction()` | Nest one agent as a tool inside another |

## Creating an Agent

```csharp
// From Azure OpenAI
AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    .GetChatClient("gpt-4o-mini")
    .AsAIAgent(
        instructions: "You are a helpful assistant.",
        name: "MyAgent",
        tools: [myTool1, myTool2]);

// From Anthropic
AIAgent agent = new AnthropicClient() { ApiKey = apiKey }
    .AsAIAgent(model: "claude-3-5-sonnet-latest", instructions: "...");

// From Ollama (via IChatClient)
AIAgent agent = new OllamaApiClient(new Uri("http://localhost:11434"))
    .AsChatClient("llama3")
    .AsAIAgent(instructions: "...");
```

## Function Tools

```csharp
// Inline delegate with description attributes
AIFunction tool = AIFunctionFactory.Create(
    ([Description("City name")] string city) => $"Sunny in {city}",
    name: "get_weather",
    description: "Returns current weather for a city.");

// From a method
AIFunction tool = AIFunctionFactory.Create(MyClass.MyStaticMethod);
```

## Running an Agent

```csharp
// Single turn, non-streaming
string result = await agent.RunAsync("What is today's date?");

// Streaming (token-by-token)
await foreach (var update in agent.RunStreamingAsync("Tell me a story"))
{
    Console.Write(update.Text);
}
```

## Multi-Agent Composition

```csharp
AIAgent innerAgent = ...; // specialized agent
AIAgent outerAgent = client.GetChatClient("gpt-4o")
    .AsAIAgent(tools: [innerAgent.AsAIFunction()]);
```

## When to Use Agent vs Workflow

| Use Agent when | Use Workflow when |
|---|---|
| Task is open-ended or conversational | Process has well-defined, explicit steps |
| Autonomous tool use / planning needed | Multiple agents must coordinate with ordering |
| Single LLM call (possibly with tools) | Need checkpointing, resumption, human-in-the-loop |

## Provider NuGet Packages

| Provider | Package |
|---|---|
| Azure OpenAI | `Azure.AI.OpenAI` + `Microsoft.Agents.AI.OpenAI` |
| Anthropic | `Microsoft.Agents.AI.Anthropic` |
| Ollama | `Microsoft.Extensions.AI.Ollama` |
| OpenAI | `OpenAI` + `Microsoft.Agents.AI.OpenAI` |

## Tool Support by Provider

| Tool Type | Chat Completion | Responses | Anthropic | Ollama |
|---|---|---|---|---|
| Function Tools | ✅ | ✅ | ✅ | ✅ |
| Local MCP Tools | ✅ | ✅ | ✅ | ✅ |
| Web Search | ✅ | ✅ | ❌ | ❌ |
| Code Interpreter | ❌ | ✅ | ✅ | ❌ |
| Hosted MCP | ❌ | ✅ | ✅ | ❌ |

## Workflows (Multi-Step Orchestration)

```csharp
// Executors = processing units (agent or custom logic)
// Edges = typed connections between executors
// WorkflowBuilder ties them into a directed graph
```

See: https://learn.microsoft.com/en-us/agent-framework/workflows/?pivots=programming-language-csharp  
Samples: https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows
