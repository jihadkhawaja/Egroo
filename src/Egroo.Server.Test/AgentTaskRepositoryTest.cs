using Egroo.Server.Database;
using Egroo.Server.Models;
using Egroo.Server.Repository;
using Egroo.Server.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Egroo.Server.Test;

[TestClass]
public class AgentTaskRepositoryTest
{
    [TestMethod]
    public async Task TwoConcurrentSubmissionsWithSameKeyCreateOneTask()
    {
        using var fixture = await Fixture.CreateAsync();
        var insertBarrier = new InsertBarrier();
        await using var first = fixture.Open(insertBarrier);
        await using var second = fixture.Open(insertBarrier);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var requestId = Guid.NewGuid();

        var results = await Task.WhenAll(
            fixture.Repository(first).CreateAsync(fixture.Agent.UserId, fixture.Agent.Id, requestId, "same task", timeout.Token),
            fixture.Repository(second).CreateAsync(fixture.Agent.UserId, fixture.Agent.Id, requestId, "same task", timeout.Token));

        CollectionAssert.AreEquivalent(
            new[] { AgentTaskSubmission.Created, AgentTaskSubmission.Existing },
            results.Select(result => result.Status).ToArray());
        var firstTask = results[0].Task;
        Assert.IsNotNull(firstTask);
        Assert.AreEqual(firstTask.Id, results[1].Task?.Id);

        await using var read = fixture.Open();
        Assert.AreEqual(1, await read.AgentTasks.CountAsync(timeout.Token));
    }

    [TestMethod]
    public async Task OneWorkerClaimsEachTaskAndTerminalStatesCannotBeOverwritten()
    {
        using var fixture = await Fixture.CreateAsync();
        await using var first = fixture.Open();
        await using var second = fixture.Open();
        var repository = fixture.Repository(first);
        var competingRepository = fixture.Repository(second);
        var created = await fixture.QueueAsync(repository, "one claimed task");
        var limit = TimeSpan.FromMinutes(5);
        var claims = await Task.WhenAll(
            Task.Run(() => repository.TryClaimNextAsync(limit, CancellationToken.None)),
            Task.Run(() => competingRepository.TryClaimNextAsync(limit, CancellationToken.None)));
        var claimed = claims.OfType<AgentTaskRecord>().Single();
        Assert.AreEqual(created.Id, claimed.Id);
        Assert.AreEqual(AgentTaskStatus.Running, claimed.Status);
        Assert.AreEqual(fixture.Clock.GetUtcNow(), claimed.StartedAt);

        Assert.IsTrue(await repository.CompleteAsync(claimed.Id, "synthetic result", CancellationToken.None));
        Assert.IsFalse(await repository.FailAsync(claimed.Id, AgentTaskFailure.ExecutionFailed, CancellationToken.None));
        var completed = await repository.CancelAsync(fixture.Agent.UserId, claimed.Id, CancellationToken.None);
        Assert.IsNotNull(completed);
        Assert.AreEqual(AgentTaskStatus.Completed, completed.Status);
        Assert.AreEqual("synthetic result", completed.Result);

        var next = await fixture.QueueAsync(repository, "cancel this task");
        Assert.AreEqual(next.Id, (await repository.TryClaimNextAsync(limit, CancellationToken.None))?.Id);
        var cancelled = await competingRepository.CancelAsync(fixture.Agent.UserId, next.Id, CancellationToken.None);
        Assert.IsNotNull(cancelled);
        Assert.AreEqual(AgentTaskStatus.Cancelled, cancelled.Status);
        Assert.IsNotNull(cancelled.StartedAt);
        Assert.IsNotNull(cancelled.FinishedAt);
        Assert.IsFalse(await repository.CompleteAsync(next.Id, "late result", CancellationToken.None));
    }

    [TestMethod]
    public async Task RestartPreservesQueuedTaskAndExpiresInterruptedClaimWithoutAnotherRun()
    {
        using var fixture = await Fixture.CreateAsync();
        var executionLimit = TimeSpan.FromMinutes(10);
        AgentTaskInfo running;
        AgentTaskInfo waiting;
        await using (var writer = fixture.Open())
        {
            var repository = fixture.Repository(writer);
            running = await fixture.QueueAsync(repository, "first");
            fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            waiting = await fixture.QueueAsync(repository, "second");
            Assert.AreEqual(running.Id, (await repository.TryClaimNextAsync(executionLimit, CancellationToken.None))?.Id);
        }

        await using var reopened = fixture.Open();
        var restarted = fixture.Repository(reopened);
        Assert.AreEqual(AgentTaskStatus.Queued, (await restarted.GetAsync(fixture.Agent.UserId, waiting.Id, CancellationToken.None))?.Status);
        Assert.AreEqual(0, await restarted.ExpireRunningAsync(CancellationToken.None));
        fixture.Clock.Advance(executionLimit);
        Assert.IsFalse(await restarted.CompleteAsync(running.Id, "expired result", CancellationToken.None));
        Assert.AreEqual(1, await restarted.ExpireRunningAsync(CancellationToken.None));
        var expired = await restarted.GetAsync(fixture.Agent.UserId, running.Id, CancellationToken.None);
        Assert.IsNotNull(expired);
        Assert.AreEqual(AgentTaskStatus.Failed, expired.Status);
        Assert.AreEqual(AgentTaskFailure.Interrupted, expired.Failure);
        Assert.IsNull(expired.Result);
        Assert.AreEqual(waiting.Id, (await restarted.TryClaimNextAsync(executionLimit, CancellationToken.None))?.Id);

        Assert.IsTrue(await restarted.FailAsync(waiting.Id, AgentTaskFailure.ExecutionFailed, CancellationToken.None));
        var failed = await restarted.GetAsync(fixture.Agent.UserId, waiting.Id, CancellationToken.None);
        Assert.AreEqual(AgentTaskFailure.ExecutionFailed, failed?.Failure);
        Assert.IsNull(await restarted.TryClaimNextAsync(executionLimit, CancellationToken.None));
    }

    [TestMethod]
    public async Task DeactivatedAgentCannotStartQueuedWork()
    {
        using var fixture = await Fixture.CreateAsync();
        await using var database = fixture.Open();
        var repository = fixture.Repository(database);
        var task = await fixture.QueueAsync(repository, "queued before deactivation");
        await database.AgentDefinitions.Where(agent => agent.Id == fixture.Agent.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(agent => agent.IsActive, false));

        Assert.IsNull(await repository.TryClaimNextAsync(TimeSpan.FromMinutes(10), CancellationToken.None));
        var failed = await repository.GetAsync(fixture.Agent.UserId, task.Id, CancellationToken.None);
        Assert.AreEqual(AgentTaskStatus.Failed, failed?.Status);
        Assert.AreEqual(AgentTaskFailure.AgentUnavailable, failed?.Failure);
        Assert.IsNull(failed?.StartedAt);
    }

    [TestMethod]
    public async Task WorkerExecutesOneClaimAcrossTwoInstancesAndStoresOnlyItsResult()
    {
        using var fixture = await Fixture.CreateAsync();
        await using var database = fixture.Open();
        var repository = fixture.Repository(database);
        var task = await fixture.QueueAsync(repository, "worker success");
        await using var services = fixture.WorkerServices();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var started = new TaskCompletionSource<AgentTaskRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var first = CreateWorker(services, fixture.Clock, (claimed, token) =>
        {
            started.SetResult(claimed);
            return finish.Task.WaitAsync(token);
        });
        int otherRuns = 0;
        using var second = CreateWorker(services, fixture.Clock, (_, _) =>
        {
            otherRuns++;
            return Task.FromResult<string?>("another worker result");
        });

        var processing = first.ProcessNextAsync(timeout.Token);
        var claimed = await started.Task.WaitAsync(timeout.Token);
        Assert.AreEqual(task.Id, claimed.Id);
        Assert.AreEqual(task.Prompt, claimed.Prompt);
        Assert.IsFalse(await second.ProcessNextAsync(timeout.Token));
        Assert.AreEqual(0, otherRuns);

        finish.SetResult("synthetic model output");
        Assert.IsTrue(await processing.WaitAsync(timeout.Token));
        var stored = await repository.GetAsync(fixture.Agent.UserId, task.Id, timeout.Token);
        Assert.AreEqual(AgentTaskStatus.Completed, stored?.Status);
        Assert.AreEqual("synthetic model output", stored?.Result);
        Assert.IsFalse(await database.AgentConversations.AnyAsync(timeout.Token));
        Assert.IsFalse(await database.AgentConversationMessages.AnyAsync(timeout.Token));
        Assert.IsFalse(await database.Messages.AnyAsync(timeout.Token));
    }

    [TestMethod]
    [DataRow(true, AgentTaskFailure.ExecutionFailed)]
    [DataRow(false, AgentTaskFailure.AgentUnavailable)]
    public async Task WorkerPersistsTypedFailure(bool providerFailed, AgentTaskFailure expected)
    {
        using var fixture = await Fixture.CreateAsync();
        await using var database = fixture.Open();
        var repository = fixture.Repository(database);
        var task = await fixture.QueueAsync(repository, "worker failure");
        await using var services = fixture.WorkerServices();
        using var worker = CreateWorker(services, fixture.Clock, (_, _) => providerFailed
            ? Task.FromException<string?>(new InvalidOperationException("test model error"))
            : Task.FromResult<string?>(null));

        Assert.IsTrue(await worker.ProcessNextAsync(CancellationToken.None));

        var stored = await repository.GetAsync(fixture.Agent.UserId, task.Id, CancellationToken.None);
        Assert.AreEqual(AgentTaskStatus.Failed, stored?.Status);
        Assert.AreEqual(expected, stored?.Failure);
        Assert.IsNull(stored?.Result);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task WorkerStopsAwaitingAnExpiredOrCancelledClaimAndDiscardsLateOutput(bool cancel)
    {
        using var fixture = await Fixture.CreateAsync();
        await using var database = fixture.Open();
        var repository = fixture.Repository(database);
        var task = await fixture.QueueAsync(repository, "worker interruption");
        await using var services = fixture.WorkerServices();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateOutput = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var worker = CreateWorker(services, fixture.Clock, (_, token) =>
        {
            started.SetResult(token);
            return lateOutput.Task;
        });

        var processing = worker.ProcessNextAsync(timeout.Token);
        var providerToken = await started.Task.WaitAsync(timeout.Token);
        if (cancel)
            await repository.CancelAsync(fixture.Agent.UserId, task.Id, timeout.Token);
        else
            fixture.Clock.Advance(AgentTaskWorker.ExecutionLimit + TimeSpan.FromSeconds(1));

        Assert.IsTrue(await processing.WaitAsync(timeout.Token));
        Assert.IsTrue(providerToken.IsCancellationRequested);
        lateOutput.SetResult("late output");
        var stored = await repository.GetAsync(fixture.Agent.UserId, task.Id, timeout.Token);
        Assert.AreEqual(cancel ? AgentTaskStatus.Cancelled : AgentTaskStatus.Failed, stored?.Status);
        Assert.AreEqual(cancel ? null : AgentTaskFailure.Interrupted, stored?.Failure);
        Assert.IsNull(stored?.Result);
        Assert.IsFalse(await worker.ProcessNextAsync(timeout.Token));
    }

    private static AgentTaskWorker CreateWorker(ServiceProvider services, TimeProvider clock,
        Func<AgentTaskRecord, CancellationToken, Task<string?>> execute) =>
        new(services.GetRequiredService<IServiceScopeFactory>(), new FakeExecutor(execute),
            clock, NullLogger<AgentTaskWorker>.Instance);

    private sealed class FakeExecutor(Func<AgentTaskRecord, CancellationToken, Task<string?>> execute) : IAgentTaskExecutor
    {
        public Task<string?> ExecuteAsync(AgentTaskRecord task, CancellationToken cancellationToken) =>
            execute(task, cancellationToken);
    }

    private sealed class InsertBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrived) == 2)
                _ready.SetResult();

            await _ready.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class TestClock : TimeProvider
    {
        private long _utcTicks = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).UtcTicks;

        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);
        public void Advance(TimeSpan period) => Interlocked.Add(ref _utcTicks, period.Ticks);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"egroo-task-repo-{Guid.NewGuid():N}.db");
        internal AgentDefinition Agent { get; } = new() { UserId = Guid.NewGuid(), Name = "Task agent" };
        internal TestClock Clock { get; } = new();

        internal static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            try
            {
                await using var database = fixture.Open();
                await database.Database.MigrateAsync();
                database.AgentDefinitions.Add(fixture.Agent);
                await database.SaveChangesAsync();
                return fixture;
            }
            catch
            {
                fixture.Dispose();
                throw;
            }
        }

        internal SqliteDataContext Open(SaveChangesInterceptor? interceptor = null)
        {
            var connectionString = new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString();
            var options = new DbContextOptionsBuilder<SqliteDataContext>().UseSqlite(connectionString);
            if (interceptor is not null)
                options.AddInterceptors(interceptor);
            return new SqliteDataContext(options.Options);
        }

        internal AgentTaskRepository Repository(DataContext database) => new(database, Clock);

        internal ServiceProvider WorkerServices() => new ServiceCollection()
            .AddScoped<DataContext>(_ => Open())
            .AddSingleton<TimeProvider>(Clock)
            .AddScoped<AgentTaskRepository>()
            .BuildServiceProvider();

        internal async Task<AgentTaskInfo> QueueAsync(AgentTaskRepository repository, string prompt)
        {
            var result = await repository.CreateAsync(Agent.UserId, Agent.Id, Guid.NewGuid(), prompt, CancellationToken.None);
            Assert.AreEqual(AgentTaskSubmission.Created, result.Status);
            Assert.IsNotNull(result.Task);
            return result.Task;
        }

        public void Dispose() => File.Delete(_path);
    }
}
