using Egroo.Contracts.Models;
using Egroo.Server.Helpers;
using Egroo.Server.Models;
using Egroo.Server.Security;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Egroo.Server.Repository;

public partial class AuthRepository
{
    private const int MaxFactorAttempts = 5;
    private static readonly TimeSpan EnrollmentLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan FactorLockout = TimeSpan.FromMinutes(5);
    private const string FactorPrompt = "Enter an authenticator code or a recovery code.";
    private const string RejectedFactor = "The verification code was not accepted.";
    private const string SaveFailure = "Could not update two-factor authentication. Try again.";
    private const string IncorrectPassword = "Incorrect current password.";

    public async Task<TwoFactor.Status> GetTwoFactorStatus() =>
        new((await GetConnectedUser())?.UserSecuriy?.IsTwoFactorEnabled == true);

    public async Task<TwoFactor.Enrollment> BeginTwoFactorEnrollment(string password)
    {
        var user = await GetConnectedUser();
        if (user is null || !HasCorrectPassword(user, password))
            return new(false, IncorrectPassword);

        if (user.UserSecuriy?.IsTwoFactorEnabled == true)
            return new(false, "Two-factor authentication is already enabled.");

        var encryptionKey = _configuration["Encryption:Key"];
        if (string.IsNullOrWhiteSpace(encryptionKey))
            return new(false, "Two-factor authentication is unavailable.");

        var state = user.UserSecuriy ??= new UserSecurity();
        byte[] secret = TwoFactorCodes.CreateSecret();
        try
        {
            state.ProtectedTotpSecret = TwoFactorCodes.ProtectSecret(secret, encryptionKey, user.Id);
            state.TotpEnrollmentExpiresAt = (_timeProvider.GetUtcNow() + EnrollmentLifetime).ToUnixTimeSeconds();
            state.LastTotpStep = null;
            state.RecoveryCodeHashes = null;
            ResetFactorAttempts(state);
            state.FactorRevision++;
            if (!await TrySaveFactorStateAsync())
                return new(false, SaveFailure);

            return new(true, SharedKey: TwoFactorCodes.FormatSecret(secret));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public async Task<TwoFactor.Activation> EnableTwoFactor(string password, string verificationCode)
    {
        var user = await GetConnectedUser();
        if (user is null || !HasCorrectPassword(user, password))
            return new(false, IncorrectPassword);

        var state = user.UserSecuriy;
        if (state?.IsTwoFactorEnabled == true)
            return new(false, "Two-factor authentication is already enabled.");

        var now = _timeProvider.GetUtcNow();
        if (state?.TotpEnrollmentExpiresAt is not { } until || until <= now.ToUnixTimeSeconds())
            return new(false, "The setup key has expired. Start setup again.");

        long revision = state.FactorRevision;
        var failure = VerifyFactor(user.Id, state, verificationCode, now, allowRecovery: false);
        if (failure is not null)
        {
            if (revision != state.FactorRevision && !await TrySaveFactorStateAsync())
                return new(false, SaveFailure);
            return new(false, failure);
        }

        var recoveryCodes = TwoFactorCodes.CreateRecoveryCodes();
        state.RecoveryCodeHashes = string.Join('\n', recoveryCodes.Select(TwoFactorCodes.RecoveryCodeHash));
        state.IsTwoFactorEnabled = true;
        state.TotpEnrollmentExpiresAt = null;
        return await TrySaveFactorStateAsync()
            ? new(true, RecoveryCodes: recoveryCodes)
            : new(false, SaveFailure);
    }

    public async Task<Operation.Result> DisableTwoFactor(string password, string verificationCode)
    {
        var user = await GetConnectedUser();
        if (user is null || !HasCorrectPassword(user, password))
            return new() { Success = false, Message = IncorrectPassword };

        if (user.UserSecuriy is not { IsTwoFactorEnabled: true } state)
            return new() { Success = true };

        long revision = state.FactorRevision;
        var failure = VerifyFactor(user.Id, state, verificationCode, _timeProvider.GetUtcNow(), allowRecovery: true);
        if (failure is not null)
        {
            if (revision != state.FactorRevision && !await TrySaveFactorStateAsync())
                failure = SaveFailure;
            return new() { Success = false, Message = failure };
        }

        state.IsTwoFactorEnabled = false;
        state.ProtectedTotpSecret = null;
        state.TotpEnrollmentExpiresAt = null;
        state.LastTotpStep = null;
        state.RecoveryCodeHashes = null;
        return await TrySaveFactorStateAsync()
            ? new() { Success = true }
            : new() { Success = false, Message = SaveFailure };
    }

    private async Task<Operation.Response?> CheckSignInFactorAsync(User user, string? verificationCode)
    {
        if (user.UserSecuriy is not { IsTwoFactorEnabled: true } state)
            return null;

        long revision = state.FactorRevision;
        var failure = VerifyFactor(user.Id, state, verificationCode, _timeProvider.GetUtcNow(), allowRecovery: true);
        if (failure is null)
            return null;

        if (revision != state.FactorRevision && !await TrySaveFactorStateAsync())
            failure = SaveFailure;

        return new() { Success = false, RequiresTwoFactor = true, Message = failure };
    }

    private string? VerifyFactor(Guid userId, UserSecurity state, string? verificationCode,
        DateTimeOffset now, bool allowRecovery)
    {
        if (string.IsNullOrWhiteSpace(verificationCode))
            return allowRecovery ? FactorPrompt : "Enter the six-digit authenticator code.";

        if (state.FactorLockedUntil is { } lockedUntil)
        {
            if (lockedUntil > now.ToUnixTimeSeconds())
                return "Too many verification attempts. Try again in a few minutes.";
            ResetFactorAttempts(state);
        }

        var code = verificationCode.Trim();
        if (TryUseTotpCode(userId, state, code, now) || (allowRecovery && TryUseRecoveryCode(state, code)))
        {
            ResetFactorAttempts(state);
            state.FactorRevision++;
            return null;
        }

        state.FailedFactorAttempts++;
        if (state.FailedFactorAttempts >= MaxFactorAttempts)
            state.FactorLockedUntil = (now + FactorLockout).ToUnixTimeSeconds();
        state.FactorRevision++;
        return RejectedFactor;
    }

    private bool TryUseTotpCode(Guid userId, UserSecurity state, string code, DateTimeOffset now)
    {
        if (code.Length != 6 || !code.All(char.IsAsciiDigit)
            || state.ProtectedTotpSecret is not { } protectedSecret
            || string.IsNullOrWhiteSpace(_configuration["Encryption:Key"]))
        {
            return false;
        }

        byte[]? secret = null;
        try
        {
            secret = TwoFactorCodes.UnprotectSecret(protectedSecret, _configuration["Encryption:Key"]!, userId);
            var step = TwoFactorCodes.MatchTimeStep(secret, code, now, state.LastTotpStep);
            if (step is null)
                return false;
            state.LastTotpStep = step;
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            _logger.LogWarning("Unable to read a stored two-factor secret.");
            return false;
        }
        finally
        {
            if (secret is not null)
                CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static bool TryUseRecoveryCode(UserSecurity state, string code)
    {
        if (state.RecoveryCodeHashes is not { } storedHashes || TwoFactorCodes.RecoveryCodeHash(code) is not { } hash)
            return false;

        var submitted = Encoding.ASCII.GetBytes(hash);
        var hashes = storedHashes.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        var match = hashes.FindIndex(candidate =>
            CryptographicOperations.FixedTimeEquals(submitted, Encoding.ASCII.GetBytes(candidate)));
        if (match < 0)
            return false;

        hashes.RemoveAt(match);
        state.RecoveryCodeHashes = hashes.Count == 0 ? null : string.Join('\n', hashes);
        return true;
    }

    private static void ResetFactorAttempts(UserSecurity state)
    {
        state.FailedFactorAttempts = 0;
        state.FactorLockedUntil = null;
    }

    private static bool HasCorrectPassword(User user, string password) =>
        !string.IsNullOrEmpty(password) && !string.IsNullOrWhiteSpace(user.Password)
        && CryptographyHelper.ComparePassword(password, user.Password);

    private async Task<bool> TrySaveFactorStateAsync()
    {
        try
        {
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            _logger.LogWarning("Could not persist a two-factor authentication change.");
            return false;
        }
    }
}
