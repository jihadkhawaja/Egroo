using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Egroo.Server.Security;

internal static class TwoFactorCodes
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int SecretSize = 20;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int PeriodSeconds = 30;
    private const int RecoveryCodeCount = 10;
    private const int RecoveryCodeLength = 20;

    public static byte[] CreateSecret() => RandomNumberGenerator.GetBytes(SecretSize);

    public static string FormatSecret(ReadOnlySpan<byte> secret)
    {
        var result = new StringBuilder((secret.Length * 8 + 4) / 5);
        int bits = 0;
        int buffer = 0;
        foreach (byte value in secret)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                result.Append(Base32Alphabet[(buffer >> bits) & 31]);
            }
        }

        if (bits != 0)
            result.Append(Base32Alphabet[(buffer << (5 - bits)) & 31]);

        return result.ToString();
    }

    public static string ProtectSecret(byte[] secret, string encryptionKey, Guid userId)
    {
        byte[] payload = new byte[NonceSize + TagSize + SecretSize];
        Span<byte> nonce = payload.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        byte[] key = DeriveProtectionKey(encryptionKey);
        try
        {
            using var cipher = new AesGcm(key, TagSize);
            cipher.Encrypt(nonce, secret, payload.AsSpan(NonceSize + TagSize),
                payload.AsSpan(NonceSize, TagSize), userId.ToByteArray());
            return Convert.ToBase64String(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static byte[] UnprotectSecret(string protectedSecret, string encryptionKey, Guid userId)
    {
        byte[] payload = Convert.FromBase64String(protectedSecret);
        if (payload.Length != NonceSize + TagSize + SecretSize)
            throw new CryptographicException("Invalid two-factor secret.");

        byte[] key = DeriveProtectionKey(encryptionKey);
        byte[] secret = new byte[SecretSize];
        try
        {
            using var cipher = new AesGcm(key, TagSize);
            cipher.Decrypt(payload.AsSpan(0, NonceSize), payload.AsSpan(NonceSize + TagSize),
                payload.AsSpan(NonceSize, TagSize), secret, userId.ToByteArray());
            return secret;
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(secret);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] DeriveProtectionKey(string encryptionKey)
    {
        byte[] source = Encoding.UTF8.GetBytes(encryptionKey);
        try
        {
            return HMACSHA256.HashData(source, "Egroo two-factor secret v1"u8);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(source);
        }
    }

    public static long? MatchTimeStep(byte[] secret, string code, DateTimeOffset now, long? lastUsed)
    {
        if (code.Length != 6 || !code.All(char.IsAsciiDigit))
            return null;

        long current = now.ToUnixTimeSeconds() / PeriodSeconds;
        var submitted = Encoding.ASCII.GetBytes(code);
        foreach (long step in new[] { current, current - 1, current + 1 })
        {
            if (step >= 0 && (lastUsed is null || step > lastUsed)
                && CryptographicOperations.FixedTimeEquals(submitted, Encoding.ASCII.GetBytes(CodeAt(secret, step))))
            {
                return step;
            }
        }
        return null;
    }

    internal static string CodeAt(ReadOnlySpan<byte> secret, long step)
    {
        Span<byte> counter = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(secret, counter, hash);
        int offset = hash[^1] & 15;
        int value = BinaryPrimitives.ReadInt32BigEndian(hash[offset..]) & int.MaxValue;
        return (value % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    public static string[] CreateRecoveryCodes()
    {
        var codes = new string[RecoveryCodeCount];
        for (var index = 0; index < codes.Length; index++)
        {
            string code = Convert.ToHexString(RandomNumberGenerator.GetBytes(RecoveryCodeLength / 2));
            codes[index] = string.Join("-", Enumerable.Range(0, 5).Select(part => code.Substring(part * 4, 4)));
        }
        return codes;
    }

    public static string? RecoveryCodeHash(string code)
    {
        if (code.Length is < RecoveryCodeLength or > RecoveryCodeLength + 4)
            return null;
        var normalized = code.Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return normalized.Length == RecoveryCodeLength && normalized.All(char.IsAsciiHexDigit)
            ? Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(normalized)))
            : null;
    }
}
