using Egroo.Contracts.Helpers;
using Egroo.Contracts.Models;
using Egroo.Server.Helpers;
using Egroo.Server.Models;
using Egroo.Server.Security;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Egroo.Server.Repository;

public partial class AuthRepository
{
    public const string RecoveryRequested = "If those details match a verified recovery address, a code will be sent.";
    private const string InvalidReset = "The recovery details or code were not accepted. Request a new code if needed.";
    private const string InvalidConfirmation = "The confirmation code was not accepted. Request a new code if needed.";
    private const string RecoveryUnavailable = "Recovery email is unavailable on this server.";
    private const string RecoverySaveFailure = "Could not update account recovery. Try again.";
    private const string ConfirmationPurpose = "email confirmation";
    private const string ResetPurpose = "password reset";
    private const long SendCooldownSeconds = 60;
    private const long CodeLifetimeSeconds = 900;

    internal AccountRecovery.Availability GetRecoveryAvailability() => new(CanSendRecoveryMail);

    private bool CanSendRecoveryMail => _recoveryEmail?.Available == true && !string.IsNullOrWhiteSpace(_configuration["Encryption:Key"]);

    public async Task<AccountRecovery.Status> GetRecoveryStatus()
    {
        var user = await GetConnectedUser();
        var state = user is null ? null : await FindRecoveryStateAsync(user.Id);
        return new(CanSendRecoveryMail, state?.ProtectedAddress is not null,
            state?.ConfirmationExpiresAt > _timeProvider.GetUtcNow().ToUnixTimeSeconds(),
            user?.UserSecuriy?.IsTwoFactorEnabled == true);
    }

    public async Task<Operation.Result> BeginRecoveryEmailEnrollment(
        string address, string password, string? verificationCode)
    {
        if (!CanSendRecoveryMail)
            return RecoveryFailure(RecoveryUnavailable);

        var user = await GetConnectedUser();
        if (user is null || !HasCorrectPassword(user, password))
            return RecoveryFailure(IncorrectPassword);

        var normalized = AccountRecoveryCodes.NormalizeAddress(address);
        if (normalized is null)
            return RecoveryFailure("Enter a valid email address.");

        long now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var state = await FindRecoveryStateAsync(user.Id);
        if (state?.ConfirmationSentAt is { } last && now - last < SendCooldownSeconds)
            return RecoveryFailure("Wait a minute before requesting another confirmation code.");

        if (await CheckRecoveryFactorAsync(user, verificationCode) is { } factorFailure)
            return RecoveryFailure(factorFailure);

        if (state is null)
        {
            state = new UserRecovery { UserId = user.Id };
            _dbContext.UserRecoveryStates.Add(state);
        }
        string code = AccountRecoveryCodes.CreateCode();
        state.ProtectedPendingAddress = AccountRecoveryCodes.ProtectAddress(normalized, _configuration["Encryption:Key"]!, user.Id);
        state.ConfirmationHash = AccountRecoveryCodes.HashCode(ConfirmationPurpose, user.Id, user.Password, code);
        state.ConfirmationExpiresAt = now + CodeLifetimeSeconds;
        state.ConfirmationSentAt = now;
        state.Revision++;
        if (!await SaveRecoveryStateAsync())
            return RecoveryFailure(RecoverySaveFailure);

        return await DeliverRecoveryEmailAsync(normalized, "Confirm your Egroo recovery address",
            $"Your confirmation code is {code}. It expires in 15 minutes. Enter it in Egroo Settings.")
            ? new() { Success = true }
            : RecoveryFailure("Could not send the confirmation email. Wait a minute and try again.");
    }

    public async Task<Operation.Result> ConfirmRecoveryEmail(string code)
    {
        var user = await GetConnectedUser();
        if (user is null)
            return RecoveryFailure(InvalidConfirmation);

        var state = await FindRecoveryStateAsync(user.Id);
        if (state?.ProtectedPendingAddress is not { } pending
            || state.ConfirmationExpiresAt is not { } expires || expires <= _timeProvider.GetUtcNow().ToUnixTimeSeconds()
            || !AccountRecoveryCodes.Matches(state.ConfirmationHash, ConfirmationPurpose, user.Id, user.Password, code))
        {
            return RecoveryFailure(InvalidConfirmation);
        }

        if (ReadRecoveryAddress(user.Id, pending) is null)
            return RecoveryFailure(RecoveryUnavailable);

        state.ProtectedAddress = pending;
        ClearConfirmation(state);
        ClearReset(state);
        state.Revision++;
        return await SaveRecoveryStateAsync() ? new() { Success = true } : RecoveryFailure(RecoverySaveFailure);
    }

    public async Task<Operation.Result> RemoveRecoveryEmail(string password, string? verificationCode)
    {
        var user = await GetConnectedUser();
        if (user is null || !HasCorrectPassword(user, password))
            return RecoveryFailure(IncorrectPassword);

        var state = await FindRecoveryStateAsync(user.Id);
        if (state is null)
            return new() { Success = true };
        if (await CheckRecoveryFactorAsync(user, verificationCode) is { } factorFailure)
            return RecoveryFailure(factorFailure);

        state.ProtectedAddress = null;
        ClearConfirmation(state);
        ClearReset(state);
        state.Revision++;
        return await SaveRecoveryStateAsync() ? new() { Success = true } : RecoveryFailure(RecoverySaveFailure);
    }

    internal async Task SendPasswordRecoveryEmailAsync(string username, string address, CancellationToken cancellationToken)
    {
        if (!CanSendRecoveryMail)
            return;

        var user = await _dbContext.Users.FirstOrDefaultAsync(account => account.Username == username, cancellationToken);
        if (user is null)
            return;

        long now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var state = await FindRecoveryStateAsync(user.Id);
        if (state?.ProtectedAddress is not { } protectedAddress
            || state.ResetExpiresAt > now
            || (state.ResetSentAt is { } last && now - last < SendCooldownSeconds)
            || !string.Equals(address, ReadRecoveryAddress(user.Id, protectedAddress), StringComparison.Ordinal))
        {
            return;
        }

        string code = AccountRecoveryCodes.CreateCode();
        state.ResetHash = AccountRecoveryCodes.HashCode(ResetPurpose, user.Id, user.Password, code);
        state.ResetExpiresAt = now + CodeLifetimeSeconds;
        state.ResetSentAt = now;
        state.Revision++;
        if (!await SaveRecoveryStateAsync())
            return;

        bool delivered = await DeliverRecoveryEmailAsync(address, "Reset your Egroo password",
            $"Your password reset code is {code}. It expires in 15 minutes. If you did not request this, you can ignore this email.",
            cancellationToken);
        if (!delivered)
        {
            ClearReset(state);
            state.Revision++;
            await SaveRecoveryStateAsync();
        }
    }

    public async Task<AccountRecovery.ResetResult> ResetPassword(string username, string code, string newPassword, string? verificationCode)
    {
        var normalized = NormalizeRecoveryUsername(username);
        if (normalized is null || string.IsNullOrWhiteSpace(code) || code.Length > 40)
            return new(false, InvalidReset);
        if (string.IsNullOrEmpty(newPassword) || !PatternMatchHelper.IsValidPassword(newPassword))
            return new(false, "Choose a password with at least 8 characters, uppercase and lowercase letters, a number, and a symbol.");

        var account = await _dbContext.Users.Where(user => user.Username == normalized)
            .Join(_dbContext.UserRecoveryStates, user => user.Id, state => state.UserId, (user, state) => new { User = user, State = state })
            .SingleOrDefaultAsync();
        var proofMatches = AccountRecoveryCodes.Matches(account?.State.ResetHash ?? new string('0', 64),
            ResetPurpose, account?.User.Id ?? Guid.Empty, account?.User.Password, code);
        if (!proofMatches || account?.State.ProtectedAddress is null || account.State.ResetExpiresAt is not { } expires
            || expires <= _timeProvider.GetUtcNow().ToUnixTimeSeconds())
        {
            return new(false, InvalidReset);
        }
        var user = account.User;
        var state = account.State;

        // A verified inbox proves the first factor only. Keep the account's enrolled second factor.
        if (await CheckRecoveryFactorAsync(user, verificationCode) is { } factorFailure)
            return new(false, factorFailure, RequiresTwoFactor: true);

        user.Password = CryptographyHelper.SecurePassword(newPassword);
        user.DateUpdated = _timeProvider.GetUtcNow();
        user.SessionRevision++;
        ClearConfirmation(state);
        ClearReset(state);
        state.Revision++;
        if (!await SaveRecoveryStateAsync())
            return new(false, InvalidReset);

        _accountSessions?.Revoke(user.Id, user.SessionRevision);
        if (CanSendRecoveryMail && ReadRecoveryAddress(user.Id, state.ProtectedAddress) is { } address)
        {
            await DeliverRecoveryEmailAsync(address, "Your Egroo password was reset",
                "Your Egroo password was reset using your recovery address. If you did not do this, contact your Egroo administrator.");
        }
        return new(true, "Password reset. Sign in with your new password.");
    }

    private async Task<string?> CheckRecoveryFactorAsync(User user, string? verificationCode)
    {
        if (user.UserSecuriy is not { IsTwoFactorEnabled: true } state)
            return null;

        long revision = state.FactorRevision;
        var failure = VerifyFactor(user.Id, state, verificationCode, _timeProvider.GetUtcNow(), allowRecovery: true);
        if (failure is not null && revision != state.FactorRevision && !await TrySaveFactorStateAsync())
            return SaveFailure;

        return failure;
    }

    internal static string? NormalizeRecoveryUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > 32)
            return null;
        string candidate = username.Trim();
        return PatternMatchHelper.IsValidUsername(candidate) ? candidate.ToLower() : null;
    }

    private Task<UserRecovery?> FindRecoveryStateAsync(Guid userId) =>
        _dbContext.UserRecoveryStates.FirstOrDefaultAsync(state => state.UserId == userId);

    private string? ReadRecoveryAddress(Guid userId, string protectedAddress)
    {
        if (_configuration["Encryption:Key"] is not { } key)
            return null;
        try
        {
            return AccountRecoveryCodes.UnprotectAddress(protectedAddress, key, userId);
        }
        catch (Exception error) when (error is FormatException or CryptographicException)
        {
            _logger.LogWarning("Could not read a stored recovery address.");
            return null;
        }
    }

    private async Task<bool> DeliverRecoveryEmailAsync(string address, string subject, string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _recoveryEmail!.SendAsync(address, subject, text, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            // Provider error text may include recipients or protocol details.
            _logger.LogWarning("Recovery email delivery failed.");
            return false;
        }
    }

    private async Task<bool> SaveRecoveryStateAsync()
    {
        try
        {
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            _logger.LogWarning("Could not persist an account recovery change.");
            return false;
        }
    }

    private static void ClearConfirmation(UserRecovery state)
    {
        state.ProtectedPendingAddress = null;
        state.ConfirmationHash = null;
        state.ConfirmationExpiresAt = null;
    }

    private static void ClearReset(UserRecovery state)
    {
        state.ResetHash = null;
        state.ResetExpiresAt = null;
    }

    private static Operation.Result RecoveryFailure(string message) => new() { Success = false, Message = message };
}
