using System.ComponentModel.DataAnnotations;
using Egroo.Contracts.Models;

namespace Egroo.Server.Models;

internal sealed class AgentTaskRecord
{
    internal const int MaxPromptLength = 8192;
    internal const int MaxResultLength = 65536;

    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid AgentId { get; set; }
    public Guid RequestId { get; set; }

    [MaxLength(MaxPromptLength)]
    public string Prompt { get; set; } = string.Empty;

    public AgentTaskStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset? ExecutionDeadline { get; set; }

    [MaxLength(MaxResultLength)]
    public string? Result { get; set; }

    public AgentTaskFailure? Failure { get; set; }

    internal AgentTaskInfo ToInfo() =>
        new(Id, AgentId, RequestId, Prompt, Status, CreatedAt, StartedAt, FinishedAt, Result, Failure);
}
