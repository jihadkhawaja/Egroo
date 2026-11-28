using System.Threading.Channels;
using Egroo.Server.Repository;
using Egroo.Server.Security;

namespace Egroo.Server.Services;

internal sealed class RecoveryEmailQueue(IServiceScopeFactory scopeFactory, ILogger<RecoveryEmailQueue> logger) : BackgroundService
{
    private sealed record Request(string Username, string Address, DateTimeOffset QueuedAt);

    private readonly Channel<Request> _requests = Channel.CreateBounded<Request>(new BoundedChannelOptions(128)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true
    });

    public void RequestReset(string? username, string? address)
    {
        username = AuthRepository.NormalizeRecoveryUsername(username);
        address = AccountRecoveryCodes.NormalizeAddress(address);
        if (username is not null && address is not null)
            _requests.Writer.TryWrite(new(username, address, DateTimeOffset.UtcNow));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _requests.Reader.ReadAllAsync(stoppingToken))
        {
            if (DateTimeOffset.UtcNow - request.QueuedAt > TimeSpan.FromMinutes(2))
                continue;

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<AuthRepository>();
                await repository.SendPasswordRecoveryEmailAsync(request.Username, request.Address, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                logger.LogWarning("Could not process an account recovery request.");
            }
        }
    }
}
