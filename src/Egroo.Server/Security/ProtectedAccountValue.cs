using System.Security.Cryptography;
using System.Text;

namespace Egroo.Server.Security;

internal static class ProtectedAccountValue
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static string Protect(ReadOnlySpan<byte> plaintext, string serverKey, Guid userId, ReadOnlySpan<byte> purpose)
    {
        byte[] payload = new byte[NonceSize + TagSize + plaintext.Length];
        Span<byte> nonce = payload.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        byte[] key = DeriveKey(serverKey, purpose);
        try
        {
            using var cipher = new AesGcm(key, TagSize);
            cipher.Encrypt(nonce, plaintext, payload.AsSpan(NonceSize + TagSize),
                payload.AsSpan(NonceSize, TagSize), userId.ToByteArray());
            return Convert.ToBase64String(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static byte[] Unprotect(string protectedValue, string serverKey, Guid userId, ReadOnlySpan<byte> purpose, int maxLength)
    {
        byte[] payload = Convert.FromBase64String(protectedValue);
        int plaintextLength = payload.Length - NonceSize - TagSize;
        if (plaintextLength <= 0 || plaintextLength > maxLength)
            throw new CryptographicException("Invalid protected account value.");

        byte[] key = DeriveKey(serverKey, purpose);
        byte[] plaintext = new byte[plaintextLength];
        try
        {
            using var cipher = new AesGcm(key, TagSize);
            cipher.Decrypt(payload.AsSpan(0, NonceSize), payload.AsSpan(NonceSize + TagSize),
                payload.AsSpan(NonceSize, TagSize), plaintext, userId.ToByteArray());
            return plaintext;
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] DeriveKey(string serverKey, ReadOnlySpan<byte> purpose)
    {
        byte[] source = Encoding.UTF8.GetBytes(serverKey);
        try
        {
            return HMACSHA256.HashData(source, purpose);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(source);
        }
    }
}
