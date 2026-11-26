using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
using Egroo.Server.Database;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Egroo.Server.Authorization;

public sealed class AccountSessions(IServiceScopeFactory scopeFactory, ILogger<AccountSessions> logger)
    : BackgroundService, IHubFilter
{
    internal const string RevisionClaim = "egroo_session_revision";
    private sealed record Connection(Guid UserId, long Revision, Action Abort);
    private readonly ConcurrentDictionary<string, Connection> _connections = new(StringComparer.Ordinal);

    internal static bool TryReadSession(ClaimsPrincipal? principal, out Guid userId, out long revision)
    {
        revision = 0;
        if (!Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out userId) || userId == Guid.Empty)
            return false;

        var value = principal!.FindFirstValue(RevisionClaim);
        return value is null || (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out revision) && revision >= 0);
    }

    internal static Task<long?> GetRevisionAsync(DataContext database, Guid userId, CancellationToken cancellationToken) =>
        database.Users.AsNoTracking().Where(user => user.Id == userId).Select(user => (long?)user.SessionRevision)
            .FirstOrDefaultAsync(cancellationToken);

    internal void Revoke(Guid userId, long activeRevision)
    {
        foreach (var connection in _connections.Values)
        {
            if (connection.UserId == userId && connection.Revision < activeRevision)
                connection.Abort();
        }
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        if (context.Context.User?.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        if (!TryReadSession(context.Context.User, out var userId, out var revision))
        {
            context.Context.Abort();
            return;
        }

        _connections[context.Context.ConnectionId] = new(userId, revision, context.Context.Abort);
        try
        {
            if (await IsCurrentAsync(context.ServiceProvider, context.Context, userId, revision))
                await next(context);
            else
            {
                _connections.TryRemove(context.Context.ConnectionId, out _);
                context.Context.Abort();
            }
        }
        catch
        {
            _connections.TryRemove(context.Context.ConnectionId, out _);
            throw;
        }
    }

    public Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        _connections.TryRemove(context.Context.ConnectionId, out _);
        return next(context, exception);
    }

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (context.Context.User?.Identity?.IsAuthenticated != true)
            return await next(context);

        if (!TryReadSession(context.Context.User, out var userId, out var revision)
            || !await IsCurrentAsync(context.ServiceProvider, context.Context, userId, revision))
        {
            context.Context.Abort();
            throw new HubException("Your session has expired. Sign in again.");
        }

        return await next(context);
    }

    private static async Task<bool> IsCurrentAsync(IServiceProvider services, HubCallerContext context, Guid userId, long revision) =>
        !context.ConnectionAborted.IsCancellationRequested
        && await GetRevisionAsync(services.GetRequiredService<DataContext>(), userId, context.ConnectionAborted) == revision;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var connections = _connections.Values.ToArray();
            if (connections.Length == 0)
                continue;

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<DataContext>();
                var revisions = new Dictionary<Guid, long>();
                foreach (var batch in connections.Select(connection => connection.UserId).Distinct().Chunk(256))
                {
                    var current = await database.Users.AsNoTracking().Where(user => batch.Contains(user.Id))
                        .Select(user => new { user.Id, user.SessionRevision }).ToArrayAsync(stoppingToken);
                    foreach (var user in current)
                        revisions[user.Id] = user.SessionRevision;
                }

                foreach (var connection in connections)
                {
                    if (!revisions.TryGetValue(connection.UserId, out var revision) || revision != connection.Revision)
                        connection.Abort();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                logger.LogWarning("Could not check active account session revisions.");
            }
        }
    }
}
