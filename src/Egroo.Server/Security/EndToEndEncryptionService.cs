using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using jihadkhawaja.chat.shared.Models;

namespace Egroo.Server.Security
{
    public class EndToEndEncryptionService
    {
        private readonly EncryptionService _encryptionService;

        public EndToEndEncryptionService(EncryptionService encryptionService)
        {
            _encryptionService = encryptionService;
        }

        public AgentEncryptionIdentity GenerateAgentIdentity()
        {
            using var rsa = RSA.Create(2048);
            byte[] publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            byte[] privateKeyBytes = rsa.ExportPkcs8PrivateKey();
            string keyId = Convert.ToHexString(SHA256.HashData(publicKeyBytes)).ToLowerInvariant()[..32];

            return new AgentEncryptionIdentity(
                Convert.ToBase64String(publicKeyBytes),
                _encryptionService.Encrypt(Convert.ToBase64String(privateKeyBytes)),
                keyId,
                DateTimeOffset.UtcNow);
        }

        public string? DecryptAgentPrivateKey(string? encryptedPrivateKey)
        {
            if (string.IsNullOrWhiteSpace(encryptedPrivateKey))
            {
                return null;
            }

            return _encryptionService.Decrypt(encryptedPrivateKey);
        }

        public string? DecryptTransportContent(string? payload, string? privateKeyBase64)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return string.Empty;
            }

            if (!TryParsePayload(payload, out var envelope))
            {
                return payload;
            }

            if (string.IsNullOrWhiteSpace(privateKeyBase64))
            {
                return null;
            }

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);

                byte[] rawAesKey = rsa.Decrypt(Convert.FromBase64String(envelope.WrappedKey), RSAEncryptionPadding.OaepSHA256);
                byte[] cipherText = Convert.FromBase64String(envelope.CipherText);
                byte[] iv = Convert.FromBase64String(envelope.Iv);

                byte[] plainBytes = new byte[cipherText.Length - 16];
                byte[] tag = cipherText[^16..];
                byte[] cipherBytes = cipherText[..^16];

                using var aes = new AesGcm(rawAesKey, 16);
                aes.Decrypt(iv, cipherBytes, tag, plainBytes);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return null;
            }
        }

        public ChannelEncryptionResult EncryptForChannelRecipients(
            string plaintext,
            IEnumerable<UserDto> userRecipients,
            IEnumerable<AgentDefinition> agentRecipients)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);

            var distinctUsers = userRecipients
                .Where(x => x.Id != Guid.Empty)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToArray();

            var distinctAgents = agentRecipients
                .Where(x => x.Id != Guid.Empty)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToArray();

            string[] missingUsers = distinctUsers
                .Where(x => string.IsNullOrWhiteSpace(x.EncryptionPublicKey))
                .Select(x => x.Username ?? x.Id.ToString("D"))
                .ToArray();

            string[] missingAgents = distinctAgents
                .Where(x => string.IsNullOrWhiteSpace(x.EncryptionPublicKey))
                .Select(x => x.Name)
                .ToArray();

            if (missingUsers.Length > 0 || missingAgents.Length > 0)
            {
                var missing = missingUsers.Concat(missingAgents).ToArray();
                throw new InvalidOperationException($"Missing encryption keys for: {string.Join(", ", missing)}.");
            }

            byte[] rawAesKey = RandomNumberGenerator.GetBytes(32);
            byte[] iv = RandomNumberGenerator.GetBytes(12);
            byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] cipherBytes = new byte[plainBytes.Length];
            byte[] tag = new byte[16];

            using (var aes = new AesGcm(rawAesKey, 16))
            {
                aes.Encrypt(iv, plainBytes, cipherBytes, tag);
            }

            byte[] combinedCipher = new byte[cipherBytes.Length + tag.Length];
            Buffer.BlockCopy(cipherBytes, 0, combinedCipher, 0, cipherBytes.Length);
            Buffer.BlockCopy(tag, 0, combinedCipher, cipherBytes.Length, tag.Length);

            var userContents = distinctUsers.Select(user => new MessageRecipientContent
            {
                UserId = user.Id,
                Content = BuildPayload(user.EncryptionPublicKey!, user.EncryptionKeyId, rawAesKey, iv, combinedCipher)
            }).ToList();

            var agentContents = distinctAgents.Select(agent => new MessageAgentRecipientContent
            {
                AgentDefinitionId = agent.Id,
                Content = BuildPayload(agent.EncryptionPublicKey!, agent.EncryptionKeyId, rawAesKey, iv, combinedCipher)
            }).ToList();

            return new ChannelEncryptionResult(userContents, agentContents);
        }

        private static string BuildPayload(string publicKeyBase64, string? keyId, byte[] rawAesKey, byte[] iv, byte[] cipherBytes)
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
            byte[] wrappedKey = rsa.Encrypt(rawAesKey, RSAEncryptionPadding.OaepSHA256);

            var payload = new EncryptionEnvelope(
                1,
                "RSA-OAEP-256/A256GCM",
                keyId,
                Convert.ToBase64String(iv),
                Convert.ToBase64String(cipherBytes),
                Convert.ToBase64String(wrappedKey));

            return JsonSerializer.Serialize(payload);
        }

        private static bool TryParsePayload(string payload, out EncryptionEnvelope envelope)
        {
            envelope = default!;

            try
            {
                var parsed = JsonSerializer.Deserialize<EncryptionEnvelope>(payload);
                if (parsed is null || parsed.V != 1 || string.IsNullOrWhiteSpace(parsed.WrappedKey) || string.IsNullOrWhiteSpace(parsed.Iv) || string.IsNullOrWhiteSpace(parsed.CipherText))
                {
                    return false;
                }

                envelope = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public sealed record AgentEncryptionIdentity(string PublicKey, string EncryptedPrivateKey, string KeyId, DateTimeOffset UpdatedOn);

        public sealed record ChannelEncryptionResult(
            List<MessageRecipientContent> UserRecipientContents,
            List<MessageAgentRecipientContent> AgentRecipientContents);

        private sealed record EncryptionEnvelope(
            [property: JsonPropertyName("v")] int V,
            [property: JsonPropertyName("alg")] string Alg,
            [property: JsonPropertyName("kid")] string? Kid,
            [property: JsonPropertyName("iv")] string Iv,
            [property: JsonPropertyName("ct")] string Ct,
            [property: JsonPropertyName("wk")] string Wk)
        {
            public string WrappedKey => Wk;
            public string CipherText => Ct;
        }
    }
}