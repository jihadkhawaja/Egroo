using System.Text;
using System.Text.Json;
using Egroo.UI.Models;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.JSInterop;

namespace Egroo.UI.Services
{
    public class EndToEndEncryptionService
    {
        private const string IdentityStorageKey = "egroo.e2ee.identity.v1";
        private const string DecryptionErrorMessage = "[Unable to decrypt this message on this device.]";

        private readonly IJSRuntime _jsRuntime;
        private readonly StorageService _storageService;
        private readonly IUser _chatUserService;

        public EndToEndEncryptionService(
            IJSRuntime jsRuntime,
            StorageService storageService,
            IUser chatUserService)
        {
            _jsRuntime = jsRuntime;
            _storageService = storageService;
            _chatUserService = chatUserService;
        }

        public async Task<EncryptionIdentity?> GetLocalIdentityAsync()
        {
            string? raw = await _storageService.GetFromLocalStorage(IdentityStorageKey);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<EncryptionIdentity>(raw);
            }
            catch
            {
                return null;
            }
        }

        public async Task<EncryptionReadiness> EnsureIdentityAsync(UserDto? currentUser)
        {
            var localIdentity = await GetLocalIdentityAsync();

            if (localIdentity is null && string.IsNullOrWhiteSpace(currentUser?.EncryptionPublicKey))
            {
                localIdentity = await GenerateAndPublishIdentityAsync();
                return EncryptionReadiness.Ready(localIdentity);
            }

            if (localIdentity is not null && string.IsNullOrWhiteSpace(currentUser?.EncryptionPublicKey))
            {
                bool published = await _chatUserService.UpdateEncryptionKey(localIdentity.PublicKey, localIdentity.KeyId);
                return published
                    ? EncryptionReadiness.Ready(localIdentity)
                    : EncryptionReadiness.NotReady("Unable to publish this device's encryption key.");
            }

            if (localIdentity is null)
            {
                return EncryptionReadiness.NotReady("This device does not have your private key. Open Settings and regenerate encryption for this device.");
            }

            if (!string.Equals(localIdentity.KeyId, currentUser?.EncryptionKeyId, StringComparison.Ordinal))
            {
                return EncryptionReadiness.NotReady("This device is out of sync with your current encryption key. Regenerate encryption in Settings to continue.");
            }

            return EncryptionReadiness.Ready(localIdentity);
        }

        public async Task<EncryptionIdentity> RegenerateIdentityAsync()
        {
            return await GenerateAndPublishIdentityAsync();
        }

        public async Task<ChannelEncryptedPayload> EncryptMessageForRecipientsAsync(
            string plaintext,
            IEnumerable<UserDto> userRecipients,
            IEnumerable<AgentDefinition>? agentRecipients = null)
        {
            var userRecipientRequests = userRecipients
                .Where(x => x.Id != Guid.Empty)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToArray();

            var agentRecipientRequests = (agentRecipients ?? Array.Empty<AgentDefinition>())
                .Where(x => x.Id != Guid.Empty)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToArray();

            string[] missingRecipients = userRecipientRequests
                .Where(x => string.IsNullOrWhiteSpace(x.EncryptionPublicKey))
                .Select(x => x.Username ?? x.Id.ToString("D"))
                .Concat(agentRecipientRequests
                    .Where(x => string.IsNullOrWhiteSpace(x.EncryptionPublicKey))
                    .Select(x => x.Name))
                .ToArray();

            if (missingRecipients.Length > 0)
            {
                throw new InvalidOperationException($"Missing encryption keys for: {string.Join(", ", missingRecipients)}.");
            }

            var payloads = await _jsRuntime.InvokeAsync<EncryptedRecipientPayload[]>(
                "egrooCrypto.encryptMessageForRecipients",
                new
                {
                    plaintext,
                    recipients = userRecipientRequests
                        .Select(x => new EncryptionRecipientRequest(x.Id, null, x.EncryptionPublicKey!, x.EncryptionKeyId))
                        .Concat(agentRecipientRequests.Select(x => new EncryptionRecipientRequest(null, x.Id, x.EncryptionPublicKey!, x.EncryptionKeyId)))
                        .ToArray()
                });

            return new ChannelEncryptedPayload(
                payloads
                    .Where(x => x.UserId.HasValue)
                    .Select(x => new MessageRecipientContent
                    {
                        UserId = x.UserId!.Value,
                        Content = x.Content
                    })
                    .ToList(),
                payloads
                    .Where(x => x.AgentDefinitionId.HasValue)
                    .Select(x => new MessageAgentRecipientContent
                    {
                        AgentDefinitionId = x.AgentDefinitionId!.Value,
                        Content = x.Content
                    })
                    .ToList());
        }

        public async Task<string?> GetDisplayContentAsync(Message message)
        {
            if (!string.IsNullOrWhiteSpace(message.DecryptedContent))
            {
                return message.DecryptedContent;
            }

            var result = await DecryptTransportContentAsync(message.Content);
            message.DecryptedContent = result.Status switch
            {
                MessageDecryptionStatus.Decrypted => result.Plaintext,
                MessageDecryptionStatus.Plaintext => result.Plaintext,
                MessageDecryptionStatus.Empty => string.Empty,
                _ => DecryptionErrorMessage,
            };

            return message.DecryptedContent;
        }

        public async Task<MessageDecryptionResult> DecryptTransportContentAsync(string? transportContent)
        {
            if (string.IsNullOrWhiteSpace(transportContent))
            {
                return new MessageDecryptionResult(MessageDecryptionStatus.Empty, string.Empty);
            }

            var identity = await GetLocalIdentityAsync();
            var result = await _jsRuntime.InvokeAsync<BrowserDecryptionResult>(
                "egrooCrypto.decryptMessage",
                new
                {
                    payload = transportContent,
                    privateKey = identity?.PrivateKey
                });

            return result.Status switch
            {
                "decrypted" => new MessageDecryptionResult(MessageDecryptionStatus.Decrypted, result.Plaintext ?? string.Empty),
                "plain" => new MessageDecryptionResult(MessageDecryptionStatus.Plaintext, result.Plaintext ?? transportContent),
                "empty" => new MessageDecryptionResult(MessageDecryptionStatus.Empty, string.Empty),
                "missing-key" => new MessageDecryptionResult(MessageDecryptionStatus.MissingKey, null),
                _ => new MessageDecryptionResult(MessageDecryptionStatus.Failed, null),
            };
        }

        public async Task<EncryptedFileUpload> EncryptFileAsync(byte[] fileBytes, string originalFileName, string? contentType)
        {
            var browserResult = await _jsRuntime.InvokeAsync<BrowserEncryptedFile>(
                "egrooCrypto.encryptFile",
                new { fileBase64 = Convert.ToBase64String(fileBytes) });

            return new EncryptedFileUpload(
                originalFileName,
                contentType ?? "application/octet-stream",
                Convert.FromBase64String(browserResult.EncryptedBase64),
                browserResult.KeyBase64,
                browserResult.IvBase64,
                fileBytes.LongLength);
        }

        public string BuildEncryptedFileToken(string absoluteUrl, EncryptedFileUpload encryptedFile)
        {
            var metadata = new EncryptedFileTokenMetadata(
                1,
                absoluteUrl,
                encryptedFile.OriginalFileName,
                encryptedFile.OriginalContentType,
                encryptedFile.OriginalSizeBytes,
                encryptedFile.KeyBase64,
                encryptedFile.IvBase64);

            string json = JsonSerializer.Serialize(metadata);
            string token = Base64UrlEncode(Encoding.UTF8.GetBytes(json));
            return $"[[egroo-file:{token}]]";
        }

        private async Task<EncryptionIdentity> GenerateAndPublishIdentityAsync()
        {
            var identity = await _jsRuntime.InvokeAsync<EncryptionIdentity>("egrooCrypto.generateIdentity");
            await SaveLocalIdentityAsync(identity);

            bool published = await _chatUserService.UpdateEncryptionKey(identity.PublicKey, identity.KeyId);
            if (!published)
            {
                throw new InvalidOperationException("Unable to publish the generated encryption key.");
            }

            return identity;
        }

        private async Task SaveLocalIdentityAsync(EncryptionIdentity identity)
        {
            await _storageService.SetLocalStorage(IdentityStorageKey, JsonSerializer.Serialize(identity));
        }

        private static string Base64UrlEncode(byte[] value)
        {
            return Convert.ToBase64String(value)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public sealed record EncryptionIdentity(string PublicKey, string PrivateKey, string KeyId);
        public sealed record EncryptionReadiness(bool IsReady, string? Message, EncryptionIdentity? Identity)
        {
            public static EncryptionReadiness Ready(EncryptionIdentity identity) => new(true, null, identity);
            public static EncryptionReadiness NotReady(string message) => new(false, message, null);
        }

        public sealed record MessageDecryptionResult(MessageDecryptionStatus Status, string? Plaintext);

        public enum MessageDecryptionStatus
        {
            Empty,
            Plaintext,
            Decrypted,
            MissingKey,
            Failed,
        }

        public sealed record EncryptedFileUpload(
            string OriginalFileName,
            string OriginalContentType,
            byte[] CipherBytes,
            string KeyBase64,
            string IvBase64,
            long OriginalSizeBytes);

        public sealed record ChannelEncryptedPayload(
            List<MessageRecipientContent> UserRecipientContents,
            List<MessageAgentRecipientContent> AgentRecipientContents);

        private sealed record EncryptionRecipientRequest(Guid? UserId, Guid? AgentDefinitionId, string PublicKey, string? KeyId);
        private sealed record EncryptedRecipientPayload(Guid? UserId, Guid? AgentDefinitionId, string Content);
        private sealed record BrowserDecryptionResult(string Status, string? Plaintext);
        private sealed record BrowserEncryptedFile(string EncryptedBase64, string KeyBase64, string IvBase64);
        private sealed record EncryptedFileTokenMetadata(int V, string Url, string FileName, string ContentType, long SizeBytes, string Key, string Iv);
    }
}