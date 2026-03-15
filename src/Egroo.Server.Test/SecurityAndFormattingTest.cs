using Egroo.Server.Helpers;
using Egroo.Server.Security;
using Egroo.Server.Services;
using Microsoft.Extensions.AI;
using System.Text;
using System.Text.Json;

namespace Egroo.Server.Test
{
    [TestClass]
    public class SecurityAndFormattingTest
    {
        [TestMethod]
        public void Normalize_RewritesEncryptedTokens_AndDataUrls()
        {
            string token = EncodeToken(new
            {
                FileName = "photo.png",
                ContentType = "image/png"
            });

            string content = $"Before [[egroo-file:{token}]] ![Preview](data:image/png;base64,abc) [Manual](data:application/pdf;base64,xyz)";

            string normalized = AgentAttachmentPromptFormatter.Normalize(content);

            StringAssert.Contains(normalized, "[Attached image: photo.png]");
            StringAssert.Contains(normalized, "[Attached image: Preview]");
            StringAssert.Contains(normalized, "[Attached file: Manual]");
        }

        [TestMethod]
        public void Normalize_ReturnsFallbackForInvalidEncryptedToken()
        {
            string normalized = AgentAttachmentPromptFormatter.Normalize("[[egroo-file:not-valid]]");

            Assert.AreEqual("[Attached file: Encrypted file]", normalized);
        }

        [TestMethod]
        public void SecurePassword_ProducesVerifiableHash()
        {
            string hash = CryptographyHelper.SecurePassword("ValidP@ss1!");

            Assert.IsTrue(CryptographyHelper.ComparePassword("ValidP@ss1!", hash));
            Assert.IsFalse(CryptographyHelper.ComparePassword("wrong-password", hash));
        }

        [TestMethod]
        public void EncryptionService_EncryptsAndDecryptsText()
        {
            var service = new EncryptionService(TestServiceProvider.EncryptionKey, TestServiceProvider.EncryptionIV);

            string cipher = service.Encrypt("secret-value");
            string plain = service.Decrypt(cipher);

            Assert.AreEqual("secret-value", plain);
        }

        [TestMethod]
        public void EndToEndEncryptionService_GeneratesDecryptableAgentIdentity()
        {
            var service = CreateEndToEndService();

            var identity = service.GenerateAgentIdentity();
            string? privateKey = service.DecryptAgentPrivateKey(identity.EncryptedPrivateKey);

            Assert.IsFalse(string.IsNullOrWhiteSpace(identity.PublicKey));
            Assert.IsFalse(string.IsNullOrWhiteSpace(privateKey));
            Assert.AreEqual(32, identity.KeyId.Length);
        }

        [TestMethod]
        public void EndToEndEncryptionService_EncryptsAndDecryptsForUsersAndAgents()
        {
            var service = CreateEndToEndService();
            var userIdentity = service.GenerateAgentIdentity();
            var agentIdentity = service.GenerateAgentIdentity();

            var result = service.EncryptForChannelRecipients(
                "hello user",
                new[]
                {
                    new UserDto
                    {
                        Id = Guid.NewGuid(),
                        Username = "repoowner",
                        EncryptionPublicKey = userIdentity.PublicKey,
                        EncryptionKeyId = userIdentity.KeyId
                    }
                },
                new[]
                {
                    new AgentDefinition
                    {
                        Id = Guid.NewGuid(),
                        Name = "Repo Agent",
                        EncryptionPublicKey = agentIdentity.PublicKey,
                        EncryptionKeyId = agentIdentity.KeyId
                    }
                },
                "hello agent");

            string? userPrivateKey = service.DecryptAgentPrivateKey(userIdentity.EncryptedPrivateKey);
            string? agentPrivateKey = service.DecryptAgentPrivateKey(agentIdentity.EncryptedPrivateKey);

            string? userPlaintext = service.DecryptTransportContent(result.UserRecipientContents.Single().Content, userPrivateKey);
            string? agentPlaintext = service.DecryptTransportContent(result.AgentRecipientContents.Single().Content, agentPrivateKey);

            Assert.AreEqual("hello user", userPlaintext);
            Assert.AreEqual("hello agent", agentPlaintext);
        }

        [TestMethod]
        public void EndToEndEncryptionService_DecryptTransportContent_HandlesPlaintextAndMissingKeys()
        {
            var service = CreateEndToEndService();

            Assert.AreEqual("plain-text", service.DecryptTransportContent("plain-text", null));
            Assert.AreEqual(string.Empty, service.DecryptTransportContent(string.Empty, null));
            Assert.IsNull(service.DecryptTransportContent("{\"v\":1,\"wk\":\"a\",\"iv\":\"b\",\"ct\":\"c\"}", null));
        }

        [TestMethod]
        public void EndToEndEncryptionService_ThrowsWhenRecipientKeyIsMissing()
        {
            var service = CreateEndToEndService();

            InvalidOperationException? exception = null;

            try
            {
                service.EncryptForChannelRecipients(
                    "hello",
                    new[]
                    {
                        new UserDto
                        {
                            Id = Guid.NewGuid(),
                            Username = "repoowner"
                        }
                    },
                    Array.Empty<AgentDefinition>());
            }
            catch (InvalidOperationException ex)
            {
                exception = ex;
            }

            Assert.IsNotNull(exception);
            StringAssert.Contains(exception.Message, "repoowner");
        }

        [TestMethod]
        public void AgentChatMessageFactory_CreateUserMessage_PreservesTextAndImageAttachments()
        {
            var message = AgentChatMessageFactory.CreateUserMessage(new AgentChatRequest
            {
                Message = "Hello\r\n[[egroo-file:not-valid]]",
                Attachments = new[]
                {
                    new AgentChatAttachment
                    {
                        FileName = "photo.png",
                        ContentType = "image/png",
                        DataUri = "data:image/png;base64,AQID"
                    },
                    new AgentChatAttachment
                    {
                        FileName = "notes.txt"
                    }
                }
            });

            Assert.AreEqual(ChatRole.User, message.Role);
            Assert.AreEqual(3, message.Contents.Count);
            Assert.AreEqual("Hello\n[Attached file: Encrypted file]", ((TextContent)message.Contents[0]).Text);
            Assert.AreEqual("image/png", ((DataContent)message.Contents[1]).MediaType);
            Assert.AreEqual("[Attached file: notes.txt]", ((TextContent)message.Contents[2]).Text);
        }

        [TestMethod]
        public void AgentChatMessageFactory_CreateStoredMessage_ConvertsInlineImagesForUserRole()
        {
            var message = AgentChatMessageFactory.CreateStoredMessage(
                ChatRole.User,
                "Before ![Diagram](data:image/png;base64,AQID) After");

            Assert.AreEqual(ChatRole.User, message.Role);
            Assert.AreEqual(3, message.Contents.Count);
            Assert.AreEqual("Before", ((TextContent)message.Contents[0]).Text);
            Assert.AreEqual("image/png", ((DataContent)message.Contents[1]).MediaType);
            Assert.AreEqual("After", ((TextContent)message.Contents[2]).Text);
        }

        [TestMethod]
        public void AgentChatMessageFactory_CreateChannelMessage_AddsSenderPrefix_AndNormalizesAssistantContent()
        {
            var userMessage = AgentChatMessageFactory.CreateChannelMessage(ChatRole.User, "repoowner", "hello");
            var assistantMessage = AgentChatMessageFactory.CreateChannelMessage(ChatRole.Assistant, "agent", "[[egroo-file:not-valid]]");

            Assert.AreEqual(2, userMessage.Contents.Count);
            Assert.AreEqual("[repoowner]: ", ((TextContent)userMessage.Contents[0]).Text);
            Assert.AreEqual("hello", ((TextContent)userMessage.Contents[1]).Text);
            Assert.AreEqual("[Attached file: Encrypted file]", assistantMessage.Text);
        }

        private static EndToEndEncryptionService CreateEndToEndService()
        {
            return new EndToEndEncryptionService(new EncryptionService(TestServiceProvider.EncryptionKey, TestServiceProvider.EncryptionIV));
        }

        private static string EncodeToken(object payload)
        {
            string json = JsonSerializer.Serialize(payload);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        // ── Additional Normalize tests ──────────────────────────────────────────────

        [TestMethod]
        public void Normalize_NullContent_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, AgentAttachmentPromptFormatter.Normalize(null));
        }

        [TestMethod]
        public void Normalize_EmptyContent_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, AgentAttachmentPromptFormatter.Normalize(""));
        }

        [TestMethod]
        public void Normalize_WhitespaceContent_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, AgentAttachmentPromptFormatter.Normalize("   "));
        }

        [TestMethod]
        public void Normalize_PlainText_ReturnsUnchanged()
        {
            Assert.AreEqual("Hello, world!", AgentAttachmentPromptFormatter.Normalize("Hello, world!"));
        }

        [TestMethod]
        public void Normalize_ImageFileToken_ReturnsAttachedImage()
        {
            string token = EncodeToken(new
            {
                FileName = "photo.png",
                ContentType = "image/jpeg"
            });

            string normalized = AgentAttachmentPromptFormatter.Normalize($"[[egroo-file:{token}]]");
            StringAssert.Contains(normalized, "[Attached image: photo.png]");
        }

        [TestMethod]
        public void Normalize_NonImageFileToken_ReturnsAttachedFile()
        {
            string token = EncodeToken(new
            {
                FileName = "document.pdf",
                ContentType = "application/pdf"
            });

            string normalized = AgentAttachmentPromptFormatter.Normalize($"[[egroo-file:{token}]]");
            StringAssert.Contains(normalized, "[Attached file: document.pdf]");
        }

        [TestMethod]
        public void Normalize_ImageDataUrl_WithEmptyAlt_UsesDefaultLabel()
        {
            string content = "![](data:image/png;base64,abc)";
            string normalized = AgentAttachmentPromptFormatter.Normalize(content);

            StringAssert.Contains(normalized, "[Attached image: Image]");
        }

        [TestMethod]
        public void Normalize_MultipleTokens_ReplacesAll()
        {
            string token1 = EncodeToken(new { FileName = "a.txt", ContentType = "text/plain" });
            string token2 = EncodeToken(new { FileName = "b.png", ContentType = "image/png" });

            string content = $"[[egroo-file:{token1}]] and [[egroo-file:{token2}]]";
            string normalized = AgentAttachmentPromptFormatter.Normalize(content);

            StringAssert.Contains(normalized, "[Attached file: a.txt]");
            StringAssert.Contains(normalized, "[Attached image: b.png]");
        }

        // ── Additional EndToEndEncryptionService tests ──────────────────────────────

        [TestMethod]
        public void EndToEndEncryptionService_DecryptAgentPrivateKey_NullInput_ReturnsNull()
        {
            var service = CreateEndToEndService();
            Assert.IsNull(service.DecryptAgentPrivateKey(null));
        }

        [TestMethod]
        public void EndToEndEncryptionService_DecryptAgentPrivateKey_EmptyInput_ReturnsNull()
        {
            var service = CreateEndToEndService();
            Assert.IsNull(service.DecryptAgentPrivateKey(""));
        }

        [TestMethod]
        public void EndToEndEncryptionService_DecryptTransportContent_NullPayload_ReturnsEmpty()
        {
            var service = CreateEndToEndService();
            Assert.AreEqual(string.Empty, service.DecryptTransportContent(null, "key"));
        }

        [TestMethod]
        public void EndToEndEncryptionService_DecryptTransportContent_NonJsonPayload_ReturnsAsIs()
        {
            var service = CreateEndToEndService();
            Assert.AreEqual("not-json", service.DecryptTransportContent("not-json", "key"));
        }

        [TestMethod]
        public void EndToEndEncryptionService_EncryptForChannelRecipients_EmptyPlaintext_Throws()
        {
            var service = CreateEndToEndService();

            Assert.ThrowsExactly<ArgumentException>(() =>
                service.EncryptForChannelRecipients(
                    "",
                    Array.Empty<UserDto>(),
                    Array.Empty<AgentDefinition>()));
        }

        [TestMethod]
        public void EndToEndEncryptionService_EncryptForChannelRecipients_SamePlaintextForUserAndAgent()
        {
            var service = CreateEndToEndService();
            var userIdentity = service.GenerateAgentIdentity();
            var agentIdentity = service.GenerateAgentIdentity();

            var result = service.EncryptForChannelRecipients(
                "shared message",
                new[]
                {
                    new UserDto
                    {
                        Id = Guid.NewGuid(),
                        Username = "user1",
                        EncryptionPublicKey = userIdentity.PublicKey,
                        EncryptionKeyId = userIdentity.KeyId
                    }
                },
                new[]
                {
                    new AgentDefinition
                    {
                        Id = Guid.NewGuid(),
                        Name = "Agent",
                        EncryptionPublicKey = agentIdentity.PublicKey,
                        EncryptionKeyId = agentIdentity.KeyId
                    }
                });

            string? userPrivateKey = service.DecryptAgentPrivateKey(userIdentity.EncryptedPrivateKey);
            string? agentPrivateKey = service.DecryptAgentPrivateKey(agentIdentity.EncryptedPrivateKey);

            Assert.AreEqual("shared message", service.DecryptTransportContent(result.UserRecipientContents.Single().Content, userPrivateKey));
            Assert.AreEqual("shared message", service.DecryptTransportContent(result.AgentRecipientContents.Single().Content, agentPrivateKey));
        }

        [TestMethod]
        public void EndToEndEncryptionService_EncryptForChannelRecipients_DuplicateRecipientsDeduped()
        {
            var service = CreateEndToEndService();
            var userIdentity = service.GenerateAgentIdentity();
            var userId = Guid.NewGuid();

            var result = service.EncryptForChannelRecipients(
                "hello",
                new[]
                {
                    new UserDto { Id = userId, Username = "user1", EncryptionPublicKey = userIdentity.PublicKey, EncryptionKeyId = userIdentity.KeyId },
                    new UserDto { Id = userId, Username = "user1", EncryptionPublicKey = userIdentity.PublicKey, EncryptionKeyId = userIdentity.KeyId }
                },
                Array.Empty<AgentDefinition>());

            Assert.AreEqual(1, result.UserRecipientContents.Count);
        }

        [TestMethod]
        public void EndToEndEncryptionService_ThrowsWhenAgentKeyIsMissing()
        {
            var service = CreateEndToEndService();

            Assert.ThrowsExactly<InvalidOperationException>(() =>
                service.EncryptForChannelRecipients(
                    "hello",
                    Array.Empty<UserDto>(),
                    new[]
                    {
                        new AgentDefinition
                        {
                            Id = Guid.NewGuid(),
                            Name = "NoKeyAgent"
                        }
                    }));
        }

        [TestMethod]
        public void EndToEndEncryptionService_DecryptTransportContent_InvalidCipherWithKey_ReturnsNull()
        {
            var service = CreateEndToEndService();
            var identity = service.GenerateAgentIdentity();
            string? privateKey = service.DecryptAgentPrivateKey(identity.EncryptedPrivateKey);

            // Valid-looking envelope but with garbage wrapped key
            string fakePayload = JsonSerializer.Serialize(new
            {
                v = 1,
                alg = "RSA-OAEP-256/A256GCM",
                kid = identity.KeyId,
                iv = Convert.ToBase64String(new byte[12]),
                ct = Convert.ToBase64String(new byte[32]),
                wk = Convert.ToBase64String(new byte[256])
            });

            var result = service.DecryptTransportContent(fakePayload, privateKey);
            Assert.IsNull(result);
        }

        // ── Additional AgentChatMessageFactory tests ────────────────────────────────

        [TestMethod]
        public void AgentChatMessageFactory_CreateStoredMessage_AssistantRole_ReturnsStringContent()
        {
            var message = AgentChatMessageFactory.CreateStoredMessage(ChatRole.Assistant, "Hello from assistant");

            Assert.AreEqual(ChatRole.Assistant, message.Role);
            Assert.AreEqual("Hello from assistant", message.Text);
        }

        [TestMethod]
        public void AgentChatMessageFactory_CreateStoredMessage_NullContent_ReturnsEmpty()
        {
            var message = AgentChatMessageFactory.CreateStoredMessage(ChatRole.Assistant, null);
            Assert.AreEqual(string.Empty, message.Text);
        }

        [TestMethod]
        public void AgentChatMessageFactory_CreateStoredMessage_UserRole_NoImages_ReturnsSingleText()
        {
            var message = AgentChatMessageFactory.CreateStoredMessage(ChatRole.User, "Plain text message");

            Assert.AreEqual(ChatRole.User, message.Role);
            Assert.AreEqual("Plain text message", message.Text);
        }

        [TestMethod]
        public void AgentChatMessageFactory_CreateChannelMessage_UserRole_PrependsName()
        {
            var message = AgentChatMessageFactory.CreateChannelMessage(ChatRole.User, "alice", "hello world");

            Assert.AreEqual(ChatRole.User, message.Role);
            Assert.AreEqual(2, message.Contents.Count);
            Assert.AreEqual("[alice]: ", ((TextContent)message.Contents[0]).Text);
        }

        [TestMethod]
        public void AgentChatMessageFactory_CreateChannelMessage_AssistantRole_NormalizesContent()
        {
            var message = AgentChatMessageFactory.CreateChannelMessage(ChatRole.Assistant, "bot", "Hello world");
            Assert.AreEqual("Hello world", message.Text);
        }

        [TestMethod]
        public void AgentChatMessageFactory_CreateUserMessage_NoAttachments_ReturnsTextOnly()
        {
            var message = AgentChatMessageFactory.CreateUserMessage(new AgentChatRequest
            {
                Message = "Just text"
            });

            Assert.AreEqual(ChatRole.User, message.Role);
            Assert.AreEqual("Just text", message.Text);
        }

        [TestMethod]
        public void AgentChatMessageFactory_CreateUserMessage_NonImageAttachment_ReturnsFileLabel()
        {
            var message = AgentChatMessageFactory.CreateUserMessage(new AgentChatRequest
            {
                Message = "See attached",
                Attachments = new[]
                {
                    new AgentChatAttachment
                    {
                        FileName = "report.pdf",
                        ContentType = "application/pdf",
                        DataUri = "data:application/pdf;base64,AQID"
                    }
                }
            });

            Assert.AreEqual(ChatRole.User, message.Role);
            Assert.IsTrue(message.Contents.Count >= 2);
        }

        [TestMethod]
        public void AgentChatMessageFactory_CreateStoredMessage_EmptyContent_User_ReturnsEmptyText()
        {
            var message = AgentChatMessageFactory.CreateStoredMessage(ChatRole.User, "");
            Assert.AreEqual(ChatRole.User, message.Role);
            Assert.AreEqual(string.Empty, message.Text);
        }

        [TestMethod]
        public void AgentChatMessageFactory_CreateChannelMessage_NullContent_ReturnsEmpty()
        {
            var message = AgentChatMessageFactory.CreateChannelMessage(ChatRole.Assistant, "bot", null);
            Assert.AreEqual(string.Empty, message.Text);
        }

        [TestMethod]
        public void AgentChatMessageFactory_CreateUserMessage_ImageAttachment_ReturnsDataContent()
        {
            // 1x1 transparent PNG, valid base64
            var png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVQIHWNgAAIABAABIniyygAAAABJRU5ErkJggg==";
            var message = AgentChatMessageFactory.CreateUserMessage(new AgentChatRequest
            {
                Message = "Check this image",
                Attachments = new[]
                {
                    new AgentChatAttachment
                    {
                        FileName = "photo.png",
                        ContentType = "image/png",
                        DataUri = $"data:image/png;base64,{png}"
                    }
                }
            });

            Assert.AreEqual(ChatRole.User, message.Role);
            Assert.IsTrue(message.Contents.Count >= 2);
            Assert.IsTrue(message.Contents.Any(c => c is DataContent));
        }

        [TestMethod]
        public void AgentChatMessageFactory_CreateUserMessage_NullAttachments_ReturnsTextOnly()
        {
            var message = AgentChatMessageFactory.CreateUserMessage(new AgentChatRequest
            {
                Message = "No attachments"
            });

            Assert.AreEqual(ChatRole.User, message.Role);
            Assert.AreEqual("No attachments", message.Text);
        }

        [TestMethod]
        public void AgentChatMessageFactory_CreateStoredMessage_UserRole_WithDataUrl_ExtractsImage()
        {
            // 1x1 transparent PNG, valid base64
            var png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVQIHWNgAAIABAABIniyygAAAABJRU5ErkJggg==";
            var content = $"Before ![Alt](data:image/png;base64,{png}) After";
            var message = AgentChatMessageFactory.CreateStoredMessage(ChatRole.User, content);

            Assert.AreEqual(ChatRole.User, message.Role);
            Assert.IsTrue(message.Contents.Count >= 2, "Should have text + data content");
        }

        // ── AgentChatResponse ───────────────────────────────────────────────────────

        [TestMethod]
        public void AgentChatResponse_Error_ReturnsFalseSuccessWithMessage()
        {
            var response = AgentChatResponse.Error("Something went wrong");

            Assert.IsFalse(response.Success);
            Assert.AreEqual("Something went wrong", response.Message);
            Assert.IsNull(response.MessageId);
            Assert.IsNull(response.ConversationId);
        }

        [TestMethod]
        public void AgentChatResponse_DefaultProperties()
        {
            var response = new AgentChatResponse();

            Assert.IsFalse(response.Success);
            Assert.AreEqual(string.Empty, response.Message);
            Assert.IsNull(response.MessageId);
            Assert.IsNull(response.ConversationId);
        }

        [TestMethod]
        public void AgentChatResponse_CanSetAllProperties()
        {
            var msgId = Guid.NewGuid();
            var convId = Guid.NewGuid();
            var response = new AgentChatResponse
            {
                Success = true,
                Message = "OK",
                MessageId = msgId,
                ConversationId = convId
            };

            Assert.IsTrue(response.Success);
            Assert.AreEqual("OK", response.Message);
            Assert.AreEqual(msgId, response.MessageId);
            Assert.AreEqual(convId, response.ConversationId);
        }

        // ── EncryptionService constructor validation ────────────────────────────────

        [TestMethod]
        public void EncryptionService_NullKey_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new EncryptionService(null, "1234567890123456"));
        }

        [TestMethod]
        public void EncryptionService_EmptyKey_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new EncryptionService("", "1234567890123456"));
        }

        [TestMethod]
        public void EncryptionService_NullIV_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new EncryptionService("12345678901234567890123456789012", null));
        }

        [TestMethod]
        public void EncryptionService_EmptyIV_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new EncryptionService("12345678901234567890123456789012", ""));
        }

        [TestMethod]
        public void EncryptionService_WrongKeyLength_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new EncryptionService("short", "1234567890123456"));
        }

        [TestMethod]
        public void EncryptionService_WrongIVLength_Throws()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new EncryptionService("12345678901234567890123456789012", "short"));
        }

        // ── Additional Normalize branch tests ───────────────────────────────────────

        [TestMethod]
        public void Normalize_TokenWithNoFileName_UsesFallback()
        {
            // Token that has ContentType but no FileName
            string token = EncodeToken(new { ContentType = "image/png" });
            string normalized = AgentAttachmentPromptFormatter.Normalize($"[[egroo-file:{token}]]");
            StringAssert.Contains(normalized, "[Attached image: Encrypted file]");
        }

        [TestMethod]
        public void Normalize_TokenWithNoContentType_ReturnsFile()
        {
            // Token with FileName but no ContentType → not image → "Attached file"
            string token = EncodeToken(new { FileName = "readme.txt" });
            string normalized = AgentAttachmentPromptFormatter.Normalize($"[[egroo-file:{token}]]");
            StringAssert.Contains(normalized, "[Attached file: readme.txt]");
        }

        // ── Multi-device v2 envelope tests ──────────────────────────────────────────

        [TestMethod]
        public void EndToEndEncryptionService_EncryptsWithMultiDeviceKeys_V2Envelope()
        {
            var service = CreateEndToEndService();
            var device1 = service.GenerateAgentIdentity();
            var device2 = service.GenerateAgentIdentity();

            var userId = Guid.NewGuid();
            var user = new UserDto
            {
                Id = userId,
                Username = "multidevice-user",
                EncryptionPublicKey = device1.PublicKey,
                EncryptionKeyId = device1.KeyId,
                EncryptionKeys = new List<UserEncryptionKeyInfo>
                {
                    new() { PublicKey = device1.PublicKey, KeyId = device1.KeyId },
                    new() { PublicKey = device2.PublicKey, KeyId = device2.KeyId },
                }
            };

            var result = service.EncryptForChannelRecipients(
                "hello multi-device",
                new[] { user },
                Array.Empty<AgentDefinition>());

            Assert.AreEqual(1, result.UserRecipientContents.Count);

            // Decrypt with device 1 private key
            string? pk1 = service.DecryptAgentPrivateKey(device1.EncryptedPrivateKey);
            string? plaintext1 = service.DecryptTransportContent(result.UserRecipientContents[0].Content, pk1);
            Assert.AreEqual("hello multi-device", plaintext1);

            // Decrypt with device 2 private key
            string? pk2 = service.DecryptAgentPrivateKey(device2.EncryptedPrivateKey);
            string? plaintext2 = service.DecryptTransportContent(result.UserRecipientContents[0].Content, pk2);
            Assert.AreEqual("hello multi-device", plaintext2);
        }

        [TestMethod]
        public void EndToEndEncryptionService_V2Envelope_WrongKey_ReturnsNull()
        {
            var service = CreateEndToEndService();
            var device1 = service.GenerateAgentIdentity();
            var device2 = service.GenerateAgentIdentity();
            var wrongDevice = service.GenerateAgentIdentity();

            var userId = Guid.NewGuid();
            var user = new UserDto
            {
                Id = userId,
                Username = "multidevice-user",
                EncryptionPublicKey = device1.PublicKey,
                EncryptionKeyId = device1.KeyId,
                EncryptionKeys = new List<UserEncryptionKeyInfo>
                {
                    new() { PublicKey = device1.PublicKey, KeyId = device1.KeyId },
                    new() { PublicKey = device2.PublicKey, KeyId = device2.KeyId },
                }
            };

            var result = service.EncryptForChannelRecipients(
                "secret message",
                new[] { user },
                Array.Empty<AgentDefinition>());

            // Decrypt with wrong private key should fail
            string? wrongPk = service.DecryptAgentPrivateKey(wrongDevice.EncryptedPrivateKey);
            string? plaintext = service.DecryptTransportContent(result.UserRecipientContents[0].Content, wrongPk);
            Assert.IsNull(plaintext);
        }

        [TestMethod]
        public void EndToEndEncryptionService_FallsBackToV1_WhenNoEncryptionKeysCollection()
        {
            var service = CreateEndToEndService();
            var identity = service.GenerateAgentIdentity();

            var user = new UserDto
            {
                Id = Guid.NewGuid(),
                Username = "legacy-user",
                EncryptionPublicKey = identity.PublicKey,
                EncryptionKeyId = identity.KeyId,
                // No EncryptionKeys collection set → falls back to v1 single-key
            };

            var result = service.EncryptForChannelRecipients(
                "legacy message",
                new[] { user },
                Array.Empty<AgentDefinition>());

            string? pk = service.DecryptAgentPrivateKey(identity.EncryptedPrivateKey);
            string? plaintext = service.DecryptTransportContent(result.UserRecipientContents[0].Content, pk);
            Assert.AreEqual("legacy message", plaintext);
        }

        [TestMethod]
        public void EndToEndEncryptionService_V2Envelope_DecryptTransportContent_NullPrivateKey_ReturnsNull()
        {
            var service = CreateEndToEndService();
            var device1 = service.GenerateAgentIdentity();

            var user = new UserDto
            {
                Id = Guid.NewGuid(),
                Username = "v2-user",
                EncryptionPublicKey = device1.PublicKey,
                EncryptionKeyId = device1.KeyId,
                EncryptionKeys = new List<UserEncryptionKeyInfo>
                {
                    new() { PublicKey = device1.PublicKey, KeyId = device1.KeyId },
                }
            };

            var result = service.EncryptForChannelRecipients(
                "test",
                new[] { user },
                Array.Empty<AgentDefinition>());

            // Decrypt with null private key
            string? plaintext = service.DecryptTransportContent(result.UserRecipientContents[0].Content, null);
            Assert.IsNull(plaintext);
        }

        [TestMethod]
        public void EndToEndEncryptionService_UserWithMultiDeviceKeys_NoLegacyKey_StillEncrypts()
        {
            var service = CreateEndToEndService();
            var device1 = service.GenerateAgentIdentity();

            var user = new UserDto
            {
                Id = Guid.NewGuid(),
                Username = "keys-only-user",
                // EncryptionPublicKey is null, but has EncryptionKeys
                EncryptionKeys = new List<UserEncryptionKeyInfo>
                {
                    new() { PublicKey = device1.PublicKey, KeyId = device1.KeyId },
                }
            };

            var result = service.EncryptForChannelRecipients(
                "keys-only message",
                new[] { user },
                Array.Empty<AgentDefinition>());

            string? pk = service.DecryptAgentPrivateKey(device1.EncryptedPrivateKey);
            string? plaintext = service.DecryptTransportContent(result.UserRecipientContents[0].Content, pk);
            Assert.AreEqual("keys-only message", plaintext);
        }
    }
}