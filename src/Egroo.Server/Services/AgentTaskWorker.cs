using Egroo.Contracts.Models;
using Egroo.Server.Models;
using Egroo.Server.Repository;

namespace Egroo.Server.Services;

internal interface IAgentTaskExecutor
{
    // A null result indicates that the agent is no longer available.
    Task<string?> ExecuteAsync(AgentTaskRecord task, CancellationToken cancellationToken);
}

internal sealed class AgentTaskWorker(
    IServiceScopeFactory scopes,
    IAgentTaskExecutor executor,
    TimeProvider clock,
    ILogger<AgentTaskWorker> logger) : BackgroundService
{
    internal static readonly TimeSpan ExecutionLimit = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await ProcessNextAsync(stoppingToken))
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "Could not process the agent task queue");
            }

            await Task.Delay(PollInterval, clock, stoppingToken);
        }
    }

    internal async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<AgentTaskRepository>();
        await repository.ExpireRunningAsync(stoppingToken);
        var task = await repository.TryClaimNextAsync(ExecutionLimit, stoppingToken);
        if (task is null)
            return false;

        await RunClaimedAsync(task, repository, stoppingToken);
        return true;
    }

    private async Task RunClaimedAsync(AgentTaskRecord task, AgentTaskRepository repository,
        CancellationToken stoppingToken)
    {
        var remaining = task.ExecutionDeadline.GetValueOrDefault() - clock.GetUtcNow();
        using var deadline = new CancellationTokenSource(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, clock);
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, deadline.Token);
        var monitor = MonitorClaimAsync(task.Id, execution);

        try
        {
            execution.Token.ThrowIfCancellationRequested();
            var result = await executor.ExecuteAsync(task, execution.Token).WaitAsync(execution.Token);
            execution.Token.ThrowIfCancellationRequested();
            if (result is null)
                await repository.FailAsync(task.Id, AgentTaskFailure.AgentUnavailable, stoppingToken);
            else
                await repository.CompleteAsync(task.Id, result, stoppingToken);
        }
        catch (OperationCanceledException) when (execution.IsCancellationRequested)
        {
            if (!stoppingToken.IsCancellationRequested)
            {
                await repository.FailAsync(task.Id, AgentTaskFailure.Interrupted, stoppingToken);
                await repository.ExpireRunningAsync(stoppingToken);
            }
        }
        catch (Exception error)
        {
            logger.LogWarning("Agent task {TaskId} execution failed with {ErrorType}", task.Id, error.GetType().Name);
            await repository.FailAsync(task.Id, AgentTaskFailure.ExecutionFailed, stoppingToken);
        }
        finally
        {
            await execution.CancelAsync();
            await monitor;
        }
    }

    private async Task MonitorClaimAsync(Guid taskId, CancellationTokenSource execution)
    {
        try
        {
            while (!execution.IsCancellationRequested)
            {
                await using (var scope = scopes.CreateAsyncScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<AgentTaskRepository>();
                    if (!await repository.IsClaimActiveAsync(taskId, execution.Token))
                    {
                        await execution.CancelAsync();
                        return;
                    }
                }

                await Task.Delay(PollInterval, clock, execution.Token);
            }
        }
        catch (OperationCanceledException) when (execution.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Could not check the execution claim for agent task {TaskId}", taskId);
            await execution.CancelAsync();
        }
    }
}
