using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace Egroo.Server.Security;

internal static class AccountRecoveryCodes
{
    public static string? NormalizeAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 254)
            return null;

        string trimmed = value.Trim();
        if (!MailAddress.TryCreate(trimmed, out var address) || !string.Equals(address.Address, trimmed, StringComparison.Ordinal))
            return null;

        return address.User + "@" + address.Host.ToLowerInvariant();
    }

    public static string CreateCode() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    public static string HashCode(string purpose, Guid userId, string? passwordHash, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}\n{userId:N}\n{passwordHash}\n{code}")));

    public static bool Matches(string? storedHash, string purpose, Guid userId, string? passwordHash, string? submitted)
    {
        if (storedHash is null || submitted is null || submitted.Length > 40)
            return false;
        string code = submitted.Trim().ToUpperInvariant();
        if (code.Length != 32 || !code.All(char.IsAsciiHexDigit))
            return false;
        string actual = HashCode(purpose, userId, passwordHash, code);
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(storedHash), Encoding.ASCII.GetBytes(actual));
    }

    private static ReadOnlySpan<byte> AddressPurpose => "Egroo private recovery email v1"u8;

    public static string ProtectAddress(string address, string serverKey, Guid userId)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(address);
        try
        {
            return ProtectedAccountValue.Protect(bytes, serverKey, userId, AddressPurpose);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public static string UnprotectAddress(string protectedAddress, string serverKey, Guid userId)
    {
        byte[] bytes = ProtectedAccountValue.Unprotect(protectedAddress, serverKey, userId, AddressPurpose, 1024);
        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
