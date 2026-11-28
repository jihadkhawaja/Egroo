namespace Egroo.Contracts.Models;

public static class AccountRecovery
{
    public sealed record Status(bool Available, bool HasVerifiedAddress, bool AwaitingVerification, bool RequiresTwoFactor = false);

    public sealed record Availability(bool Available);

    public sealed record ResetResult(bool Success, string? Message = null, bool RequiresTwoFactor = false);
}
