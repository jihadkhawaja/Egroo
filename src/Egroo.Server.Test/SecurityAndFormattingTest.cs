using System.Text;
using System.Text.Json;
using Egroo.Server.Helpers;
using Egroo.Server.Security;
using Egroo.Server.Services;
using jihadkhawaja.chat.shared.Models;
using Microsoft.Extensions.AI;

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
    }
}