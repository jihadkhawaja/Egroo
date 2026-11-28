using System.Text.Json.Serialization;

namespace Egroo.Contracts.Models;

[JsonConverter(typeof(JsonStringEnumConverter<AgentTaskStatus>))]
public enum AgentTaskStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

[JsonConverter(typeof(JsonStringEnumConverter<AgentTaskFailure>))]
public enum AgentTaskFailure
{
    AgentUnavailable = 0,
    ExecutionFailed = 1,
    Interrupted = 2
}

public sealed record CreateAgentTaskRequest(Guid AgentId, Guid RequestId, string? Prompt)
{
    public const int MaxPromptLength = 8192;
}

public sealed record AgentTaskInfo(
    Guid Id,
    Guid AgentId,
    Guid RequestId,
    string Prompt,
    AgentTaskStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? Result,
    AgentTaskFailure? Failure);
