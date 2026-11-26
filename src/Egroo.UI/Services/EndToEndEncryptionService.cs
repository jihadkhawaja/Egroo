using Egroo.Contracts.Interfaces;
using Egroo.Contracts.Models;
using System.Text;
using System.Text.Json;

namespace Egroo.UI.Services
{
    public class EndToEndEncryptionService
    {
        private const string IdentityStorageKey = "egroo.e2ee.identity.v1";
        private const string DecryptionErrorMessage = "[Unable to decrypt this message on this device.]";
        private const string MissingKeyMessage = "[This device does not have a key for this message.]";
        private const string DecryptionUnavailableMessage = "[Decryption is temporarily unavailable. Reopen the conversation to retry.]";
        private const int MaxBatchMessages = 32;
        private const int MaxBatchCharacters = 64 * 1024;
        private static readonly TimeSpan DecryptionAttemptTimeout = TimeSpan.FromSeconds(3);

        private readonly ClientRuntime _jsRuntime;
        private readonly StorageService _storageService;
        private readonly IUser _chatUserService;
        private string _identityKey = IdentityStorageKey;

        public EndToEndEncryptionService(
            ClientRuntime jsRuntime,
            StorageService storageService,
            IUser chatUserService)
        {
            _jsRuntime = jsRuntime;
            _storageService = storageService;
            _chatUserService = chatUserService;
        }

        internal void UseIdentityScope(Uri api, Guid userId) => _identityKey = $"{IdentityStorageKey}:{api.AbsoluteUri}:{userId:N}";

        public Task<EncryptionIdentity?> GetLocalIdentityAsync() => ReadIdentityAsync(_identityKey);

        private async Task<EncryptionIdentity?> ReadIdentityAsync(string key, CancellationToken cancellationToken = default)
        {
            string? raw = await _storageService.GetFromLocalStorage(key, cancellationToken);
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
            if (localIdentity is null && _identityKey != IdentityStorageKey
                && await ReadIdentityAsync(IdentityStorageKey) is { } legacy
                && IsKeyRegisteredOnServer(legacy, currentUser))
            {
                localIdentity = legacy;
                await SaveLocalIdentityAsync(legacy);
            }

            // No local key and no server keys → generate new key and register it
            if (localIdentity is null && string.IsNullOrWhiteSpace(currentUser?.EncryptionPublicKey))
            {
                localIdentity = await GenerateAndPublishIdentityAsync();
                return EncryptionReadiness.Ready(localIdentity);
            }

            // Have local key but it's not on server yet → register it as a new device key
            if (localIdentity is not null && string.IsNullOrWhiteSpace(currentUser?.EncryptionPublicKey))
            {
                bool published = await _chatUserService.AddEncryptionKey(localIdentity.PublicKey, localIdentity.KeyId, null);
                return published
                    ? EncryptionReadiness.Ready(localIdentity)
                    : EncryptionReadiness.NotReady("Unable to publish this device's encryption key.");
            }

            // No local key but server has keys → this is a new device, generate and add
            if (localIdentity is null)
            {
                localIdentity = await GenerateAndPublishIdentityAsync();
                return EncryptionReadiness.Ready(localIdentity);
            }

            // Have local key — check if it's registered on server (multi-device: check against all keys)
            bool isRegistered = IsKeyRegisteredOnServer(localIdentity, currentUser);
            if (!isRegistered)
            {
                // Local key exists but not registered on server — register it as a new device key
                bool published = await _chatUserService.AddEncryptionKey(localIdentity.PublicKey, localIdentity.KeyId, null);
                return published
                    ? EncryptionReadiness.Ready(localIdentity)
                    : EncryptionReadiness.NotReady("Unable to publish this device's encryption key.");
            }

            return EncryptionReadiness.Ready(localIdentity);
        }

        private static bool IsKeyRegisteredOnServer(EncryptionIdentity localIdentity, UserDto? currentUser)
        {
            if (currentUser is null)
            {
                return false;
            }

            // Check multi-device keys first
            if (currentUser.EncryptionKeys is { Count: > 0 })
            {
                return currentUser.EncryptionKeys.Any(k =>
                    string.Equals(k.KeyId, localIdentity.KeyId, StringComparison.Ordinal));
            }

            // Fallback to legacy single key
            return string.Equals(localIdentity.KeyId, currentUser.EncryptionKeyId, StringComparison.Ordinal);
        }

        public async Task<EncryptionIdentity> RegenerateIdentityAsync()
        {
            return await GenerateAndPublishIdentityAsync();
        }

        public async Task<ChannelEncryptedPayload> EncryptMessageForRecipientsAsync(
            string plaintext,
            IEnumerable<UserDto> userRecipients,
            IEnumerable<AgentDefinition>? agentRecipients = null,
            string? agentPlaintext = null)
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
                .Where(x => string.IsNullOrWhiteSpace(x.EncryptionPublicKey) && (x.EncryptionKeys is null || x.EncryptionKeys.Count == 0))
                .Select(x => x.Username ?? x.Id.ToString("D"))
                .Concat(agentRecipientRequests
                    .Where(x => string.IsNullOrWhiteSpace(x.EncryptionPublicKey))
                    .Select(x => x.Name))
                .ToArray();

            if (missingRecipients.Length > 0)
            {
                throw new InvalidOperationException($"Missing encryption keys for: {string.Join(", ", missingRecipients)}.");
            }

            string resolvedAgentPlaintext = string.IsNullOrWhiteSpace(agentPlaintext)
                ? plaintext
                : agentPlaintext;

            var userPayloads = userRecipientRequests.Length == 0
                ? Array.Empty<EncryptedRecipientPayload>()
                : await EncryptRecipientPayloadsAsync(
                    plaintext,
                    userRecipientRequests
                        .Select(x => BuildRecipientRequest(x))
                        .ToArray());

            var agentPayloads = agentRecipientRequests.Length == 0
                ? Array.Empty<EncryptedRecipientPayload>()
                : await EncryptRecipientPayloadsAsync(
                    resolvedAgentPlaintext,
                    agentRecipientRequests
                        .Select(x => new EncryptionRecipientRequest(null, x.Id, x.EncryptionPublicKey!, x.EncryptionKeyId, null))
                        .ToArray());

            return new ChannelEncryptedPayload(
                userPayloads
                    .Where(x => x.UserId.HasValue)
                    .Select(x => new MessageRecipientContent
                    {
                        UserId = x.UserId!.Value,
                        Content = x.Content
                    })
                    .ToList(),
                agentPayloads
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
            await PopulateDisplayContentsAsync([message], runtimeAvailable: true, CancellationToken.None);
            return message.DecryptedContent;
        }

        internal static IEnumerable<Message[]> DecryptionBatches(IEnumerable<Message> messages)
        {
            var batch = new List<Message>(MaxBatchMessages);
            var characters = 0;
            foreach (var message in messages)
            {
                var length = message.Content?.Length ?? 0;
                if (batch.Count == MaxBatchMessages || batch.Count > 0 && length > MaxBatchCharacters - characters)
                {
                    yield return batch.ToArray();
                    batch.Clear();
                    characters = 0;
                }

                batch.Add(message);
                characters += length;
            }
            if (batch.Count > 0)
                yield return batch.ToArray();
        }

        internal async Task<bool> PopulateDisplayContentsAsync(
            IReadOnlyList<Message> batch, bool runtimeAvailable, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pending = new List<Message>(batch.Count);
            foreach (var message in batch)
            {
                if (!string.IsNullOrWhiteSpace(message.DecryptedContent))
                    continue;
                if (string.IsNullOrWhiteSpace(message.Content))
                    message.DecryptedContent = string.Empty;
                else
                    pending.Add(message);
            }
            if (pending.Count == 0)
                return false;

            if (runtimeAvailable)
            {
                var payloads = pending.Select(message => message.Content!).ToArray();
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    using var attemptTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    attemptTimeout.CancelAfter(DecryptionAttemptTimeout);
                    try
                    {
                        var identity = await ReadIdentityAsync(_identityKey, attemptTimeout.Token);
                        var results = await _jsRuntime.InvokeAsync<BrowserDecryptionResult[]>(
                            "egrooCrypto.decryptMessages", attemptTimeout.Token,
                            new { payloads, privateKey = identity?.PrivateKey });
                        if (results is null || results.Length != pending.Count)
                            throw new InvalidOperationException("The decryption runtime returned an incomplete batch.");

                        for (var index = 0; index < pending.Count; index++)
                        {
                            var result = ReadDecryptionResult(results[index], payloads[index]);
                            pending[index].DecryptedContent = result.Status switch
                            {
                                MessageDecryptionStatus.Decrypted or MessageDecryptionStatus.Plaintext => result.Plaintext,
                                MessageDecryptionStatus.Empty => string.Empty,
                                MessageDecryptionStatus.MissingKey => MissingKeyMessage,
                                _ => DecryptionErrorMessage,
                            };
                        }
                        return false;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        // Runtime failures get one retry. Individual invalid messages already have their own results.
                        System.Diagnostics.Debug.WriteLine($"Message decryption runtime failure ({ex.GetType().Name}).");
                    }
                }
            }

            foreach (var message in pending)
                message.DecryptedContent = DecryptionUnavailableMessage;
            return true;
        }

        public Task<MessageDecryptionResult> DecryptTransportContentAsync(string? transportContent) =>
            DecryptTransportContentAsync(transportContent, CancellationToken.None);

        private async Task<MessageDecryptionResult> DecryptTransportContentAsync(string? transportContent, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(transportContent))
            {
                return new MessageDecryptionResult(MessageDecryptionStatus.Empty, string.Empty);
            }

            var identity = await ReadIdentityAsync(_identityKey, cancellationToken);
            var result = await _jsRuntime.InvokeAsync<BrowserDecryptionResult>(
                "egrooCrypto.decryptMessage", cancellationToken,
                new
                {
                    payload = transportContent,
                    privateKey = identity?.PrivateKey
                });

            return ReadDecryptionResult(result, transportContent);
        }

        private static MessageDecryptionResult ReadDecryptionResult(BrowserDecryptionResult? result, string transportContent) =>
            result?.Status switch
            {
                "decrypted" => new MessageDecryptionResult(MessageDecryptionStatus.Decrypted, result.Plaintext ?? string.Empty),
                "plain" => new MessageDecryptionResult(MessageDecryptionStatus.Plaintext, result.Plaintext ?? transportContent),
                "empty" => new MessageDecryptionResult(MessageDecryptionStatus.Empty, string.Empty),
                "missing-key" => new MessageDecryptionResult(MessageDecryptionStatus.MissingKey, null),
                _ => new MessageDecryptionResult(MessageDecryptionStatus.Failed, null),
            };

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

            bool published = await _chatUserService.AddEncryptionKey(identity.PublicKey, identity.KeyId, null);
            if (!published)
            {
                throw new InvalidOperationException("Unable to publish the generated encryption key.");
            }

            return identity;
        }

        private async Task SaveLocalIdentityAsync(EncryptionIdentity identity)
        {
            await _storageService.SetLocalStorage(_identityKey, JsonSerializer.Serialize(identity));
        }

        private async Task<EncryptedRecipientPayload[]> EncryptRecipientPayloadsAsync(string plaintext, EncryptionRecipientRequest[] recipients)
        {
            return await _jsRuntime.InvokeAsync<EncryptedRecipientPayload[]>(
                "egrooCrypto.encryptMessageForRecipients",
                new
                {
                    plaintext,
                    recipients
                });
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

        private static EncryptionRecipientRequest BuildRecipientRequest(UserDto user)
        {
            // Multi-device: pass all device keys for v2 envelope
            if (user.EncryptionKeys is { Count: > 0 })
            {
                var keys = user.EncryptionKeys
                    .Where(k => !string.IsNullOrWhiteSpace(k.PublicKey))
                    .Select(k => new RecipientKeyEntry(k.PublicKey, k.KeyId))
                    .ToArray();

                if (keys.Length > 0)
                {
                    return new EncryptionRecipientRequest(user.Id, null, null, null, keys);
                }
            }

            // Fallback to legacy single key
            return new EncryptionRecipientRequest(user.Id, null, user.EncryptionPublicKey, user.EncryptionKeyId, null);
        }

        private sealed record EncryptionRecipientRequest(Guid? UserId, Guid? AgentDefinitionId, string? PublicKey, string? KeyId, RecipientKeyEntry[]? Keys);
        private sealed record RecipientKeyEntry(string PublicKey, string KeyId);
        private sealed record EncryptedRecipientPayload(Guid? UserId, Guid? AgentDefinitionId, string Content);
        private sealed record BrowserDecryptionResult(string Status, string? Plaintext);
        private sealed record BrowserEncryptedFile(string EncryptedBase64, string KeyBase64, string IvBase64);
        private sealed record EncryptedFileTokenMetadata(int V, string Url, string FileName, string ContentType, long SizeBytes, string Key, string Iv);
    }
}
