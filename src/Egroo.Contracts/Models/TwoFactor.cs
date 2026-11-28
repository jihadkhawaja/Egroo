namespace Egroo.Contracts.Models;

public static class TwoFactor
{
    public sealed record Status(bool Enabled);

    public sealed record Enrollment(bool Success, string? Message = null, string? SharedKey = null);

    public sealed record Activation(bool Success, string? Message = null, string[]? RecoveryCodes = null);
}
