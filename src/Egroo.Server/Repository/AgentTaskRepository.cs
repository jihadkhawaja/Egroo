using System.Data;
using Egroo.Contracts.Models;
using Egroo.Server.Database;
using Egroo.Server.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Egroo.Server.Repository;

internal enum AgentTaskSubmission
{
    Created,
    Existing,
    Conflict,
    AgentUnavailable,
    AtCapacity,
    AdmissionBusy
}

internal sealed class AgentTaskRepository(DataContext database, TimeProvider clock)
{
    internal const int MaxActiveTasks = 5;
    private const int MaxAdmissionAttempts = 6;

    private IQueryable<AgentDefinition> ActiveAgents => database.AgentDefinitions
        .Where(agent => agent.IsActive && agent.DateDeleted == null);

    public async Task<(AgentTaskSubmission Status, AgentTaskInfo? Task)> CreateAsync(
        Guid ownerId, Guid agentId, Guid requestId, string prompt, CancellationToken cancellationToken)
    {
        var existing = await FindByRequestAsync(ownerId, requestId, cancellationToken);
        if (existing is not null)
            return ResolveDuplicate(existing, agentId, prompt);

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await AdmitAsync(ownerId, agentId, requestId, prompt, cancellationToken);
            }
            catch (Exception error) when (CanRetryAdmission(error))
            {
                if (attempt == MaxAdmissionAttempts)
                {
                    existing = await FindByRequestAsync(ownerId, requestId, cancellationToken);
                    return existing is null
                        ? (AgentTaskSubmission.AdmissionBusy, null)
                        : ResolveDuplicate(existing, agentId, prompt);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10 * attempt + Random.Shared.Next(10)), cancellationToken);
            }
        }
    }

    private async Task<(AgentTaskSubmission Status, AgentTaskInfo? Task)> AdmitAsync(
        Guid ownerId, Guid agentId, Guid requestId, string prompt, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var existing = await FindByRequestAsync(ownerId, requestId, cancellationToken);
        if (existing is not null)
            return ResolveDuplicate(existing, agentId, prompt);

        if (!await ActiveAgents.AnyAsync(agent => agent.Id == agentId && agent.UserId == ownerId, cancellationToken))
            return (AgentTaskSubmission.AgentUnavailable, null);

        int activeCount = await database.AgentTasks.Where(task => task.OwnerId == ownerId
                && (task.Status == AgentTaskStatus.Queued || task.Status == AgentTaskStatus.Running))
            .Take(MaxActiveTasks).CountAsync(cancellationToken);
        if (activeCount == MaxActiveTasks)
            return (AgentTaskSubmission.AtCapacity, null);

        var task = new AgentTaskRecord
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            AgentId = agentId,
            RequestId = requestId,
            Prompt = prompt,
            Status = AgentTaskStatus.Queued,
            CreatedAt = clock.GetUtcNow()
        };

        database.AgentTasks.Add(task);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (AgentTaskSubmission.Created, task.ToInfo());
        }
        catch
        {
            database.Entry(task).State = EntityState.Detached;
            throw;
        }
    }

    private static bool CanRetryAdmission(Exception error) => error.GetBaseException() switch
    {
        PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected
            or PostgresErrorCodes.UniqueViolation } => true,
        SqliteException { SqliteErrorCode: 5 or 6 } => true, // Busy or locked.
        SqliteException { SqliteExtendedErrorCode: 2067 } => true, // Unique constraint.
        _ => false
    };

    public async Task<AgentTaskInfo[]> ListAsync(Guid ownerId, int skip, int take, CancellationToken cancellationToken)
    {
        var tasks = await database.AgentTasks.AsNoTracking()
            .Where(task => task.OwnerId == ownerId)
            .OrderByDescending(task => task.CreatedAt).ThenByDescending(task => task.Id)
            .Skip(skip).Take(take).ToArrayAsync(cancellationToken);
        return tasks.Select(task => task.ToInfo()).ToArray();
    }

    public async Task<AgentTaskInfo?> GetAsync(Guid ownerId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await OwnedTask(ownerId, taskId).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return task?.ToInfo();
    }

    public async Task<AgentTaskInfo?> CancelAsync(Guid ownerId, Guid taskId, CancellationToken cancellationToken)
    {
        var target = OwnedTask(ownerId, taskId);
        var now = clock.GetUtcNow();
        await target.Where(task => task.Status == AgentTaskStatus.Queued || task.Status == AgentTaskStatus.Running)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(task => task.Status, AgentTaskStatus.Cancelled)
                .SetProperty(task => task.FinishedAt, now)
                .SetProperty(task => task.ExecutionDeadline, (DateTimeOffset?)null), cancellationToken);

        var task = await target.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return task?.ToInfo();
    }

    internal async Task<AgentTaskRecord?> TryClaimNextAsync(TimeSpan executionLimit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(executionLimit, TimeSpan.Zero);
        var queued = database.AgentTasks.Where(task => task.Status == AgentTaskStatus.Queued);

        while (true)
        {
            var task = await queued.AsNoTracking().OrderBy(task => task.CreatedAt).ThenBy(task => task.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (task is null)
                return null;

            var now = clock.GetUtcNow();
            var deadline = now + executionLimit;
            var candidate = queued.Where(row => row.Id == task.Id);
            var available = ActiveAgents;
            var claimed = await candidate
                .Where(row => available.Any(agent => agent.Id == row.AgentId && agent.UserId == row.OwnerId))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.Status, AgentTaskStatus.Running)
                    .SetProperty(row => row.StartedAt, now)
                    .SetProperty(row => row.ExecutionDeadline, deadline), cancellationToken);

            if (claimed == 1)
            {
                task.Status = AgentTaskStatus.Running;
                task.StartedAt = now;
                task.ExecutionDeadline = deadline;
                return task;
            }

            await candidate.Where(row => !available.Any(agent => agent.Id == row.AgentId && agent.UserId == row.OwnerId))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.Status, AgentTaskStatus.Failed)
                    .SetProperty(row => row.FinishedAt, now)
                    .SetProperty(row => row.Failure, AgentTaskFailure.AgentUnavailable), cancellationToken);
        }
    }

    internal Task<bool> CompleteAsync(Guid taskId, string result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(result.Length, AgentTaskRecord.MaxResultLength);
        return FinishAsync(taskId, AgentTaskStatus.Completed, result, null, cancellationToken);
    }

    internal Task<bool> FailAsync(Guid taskId, AgentTaskFailure failure, CancellationToken cancellationToken) =>
        FinishAsync(taskId, AgentTaskStatus.Failed, null, failure, cancellationToken);

    internal Task<bool> IsClaimActiveAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return database.AgentTasks.AnyAsync(task =>
            task.Id == taskId && task.Status == AgentTaskStatus.Running && task.ExecutionDeadline > now,
            cancellationToken);
    }

    internal Task<int> ExpireRunningAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return database.AgentTasks
            .Where(task => task.Status == AgentTaskStatus.Running
                && (task.ExecutionDeadline == null || task.ExecutionDeadline <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(task => task.Status, AgentTaskStatus.Failed)
                .SetProperty(task => task.Failure, AgentTaskFailure.Interrupted)
                .SetProperty(task => task.FinishedAt, now)
                .SetProperty(task => task.ExecutionDeadline, (DateTimeOffset?)null), cancellationToken);
    }

    private async Task<bool> FinishAsync(Guid taskId, AgentTaskStatus status, string? result,
        AgentTaskFailure? failure, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await database.AgentTasks.Where(task =>
                task.Id == taskId && task.Status == AgentTaskStatus.Running && task.ExecutionDeadline > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(task => task.Status, status)
                .SetProperty(task => task.Result, result)
                .SetProperty(task => task.Failure, failure)
                .SetProperty(task => task.FinishedAt, now)
                .SetProperty(task => task.ExecutionDeadline, (DateTimeOffset?)null), cancellationToken) == 1;
    }

    private IQueryable<AgentTaskRecord> OwnedTask(Guid ownerId, Guid taskId) =>
        database.AgentTasks.Where(task => task.Id == taskId && task.OwnerId == ownerId);

    private Task<AgentTaskRecord?> FindByRequestAsync(Guid ownerId, Guid requestId, CancellationToken cancellationToken) =>
        database.AgentTasks.AsNoTracking()
            .SingleOrDefaultAsync(task => task.OwnerId == ownerId && task.RequestId == requestId, cancellationToken);

    private static (AgentTaskSubmission Status, AgentTaskInfo? Task) ResolveDuplicate(
        AgentTaskRecord task, Guid agentId, string prompt) =>
        task.AgentId == agentId && string.Equals(task.Prompt, prompt, StringComparison.Ordinal)
            ? (AgentTaskSubmission.Existing, task.ToInfo())
            : (AgentTaskSubmission.Conflict, null);
}
