using System.Net;
using Egroo.Contracts.Models;
using Egroo.Server.Models;
using Egroo.Server.Security;

namespace Egroo.Server.Repository;

public partial class AuthRepository
{
    private const string RegionRecoveryPrompt = "Enter an unused recovery code to sign in from this region.";

    private IPAddress? CurrentPeer => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;

    private bool RequiresRegionalRecovery(UserSecurity state) =>
        _ipRegionPolicy.RequiresRecovery(state.AllowedSignInRegions, CurrentPeer);

    public async Task<IpRegionSettings.Status> GetIpRegionStatus()
    {
        var user = await GetConnectedUser();
        if (user is null)
            return new(false, false, false, null, [], []);

        var allowed = IpRegionPolicy.ReadRegions(user.UserSecuriy?.AllowedSignInRegions);
        var current = _ipRegionPolicy.Resolve(CurrentPeer);
        bool canEnable = current is not null
            && user.UserSecuriy is { IsTwoFactorEnabled: true, RecoveryCodeHashes.Length: > 0 };

        return new(_ipRegionPolicy.Available, allowed.Length > 0, canEnable, current,
            _ipRegionPolicy.Regions.ToArray(), allowed);
    }

    public async Task<Operation.Result> UpdateIpRegionSettings(IpRegionSettings.Update settings)
    {
        var user = await GetConnectedUser();
        if (user is null || !HasCorrectPassword(user, settings.Password))
            return CreateFailureResult(IncorrectPassword);

        if (settings.AllowedRegions is not { } requested)
            return CreateFailureResult("Choose allowed regions or submit an empty list to disable the restriction.");
        if (user.UserSecuriy is not { IsTwoFactorEnabled: true } state)
            return CreateFailureResult("Enable two-factor authentication first.");

        var allowed = _ipRegionPolicy.ValidateSelection(requested, CurrentPeer);
        if (allowed is null)
            return CreateFailureResult("Select configured regions that include your current region.");
        if (allowed.Length > 0 && string.IsNullOrEmpty(state.RecoveryCodeHashes))
            return CreateFailureResult("Keep at least one unused recovery code before enabling IP-region protection.");

        var revision = state.FactorRevision;
        var failure = VerifyFactor(user.Id, state, settings.VerificationCode, _timeProvider.GetUtcNow(),
            allowRecovery: allowed.Length == 0);
        if (failure is not null)
        {
            if (revision != state.FactorRevision && !await TrySaveFactorStateAsync())
                failure = SaveFailure;
            return CreateFailureResult(failure);
        }

        state.AllowedSignInRegions = allowed.Length == 0 ? null : string.Join('\n', allowed);
        return await TrySaveFactorStateAsync() ? new() { Success = true } : CreateFailureResult(SaveFailure);
    }

    private static Operation.Result CreateFailureResult(string message) =>
        new() { Success = false, Message = message };
}
