namespace Egroo.Server.Test
{
    /// <summary>
    /// Tests the message send / update / pending flow using the
    /// <c>jihadkhawaja.chat.server</c> hub backed by an in-memory EF Core database.
    /// These tests exercise <c>MessageRepository</c> which implements
    /// <see cref="IMessageRepository"/> defined in <c>jihadkhawaja.chat.shared</c>.
    /// </summary>
    [TestClass]
    public class MessageTest
    {
        private const string DbName = "MessageTestDb";

        private Guid _userId;
        private Guid _channelId;
        private IServiceProvider _authenticatedServices = null!;

        [TestInitialize]
        public async Task Initialize()
        {
            // 1. Sign up & sign in.
            var anonServices = TestServiceProvider.Build(dbName: DbName);
            using var signUpScope = anonServices.CreateScope();
            var auth = signUpScope.ServiceProvider.GetRequiredService<IAuth>();

            var signUp = await auth.SignUp("msgtester", "ValidP@ss1!");
            if (!signUp.Success && !(signUp.Message?.Contains("exist", StringComparison.OrdinalIgnoreCase) ?? false))
                Assert.Fail($"Sign-up failed unexpectedly: {signUp.Message}");

            using var signInScope = anonServices.CreateScope();
            var signIn = await signInScope.ServiceProvider.GetRequiredService<IAuth>()
                                          .SignIn("msgtester", "ValidP@ss1!");
            Assert.IsTrue(signIn.Success, $"Sign-in failed: {signIn.Message}");
            _userId = signIn.UserId!.Value;

            // 2. Build authenticated services.
            _authenticatedServices = TestServiceProvider.Build(dbName: DbName, authenticatedUserId: _userId);

            // 3. Create a channel to use in message tests.
            using var channelScope = _authenticatedServices.CreateScope();
            var channel = await channelScope.ServiceProvider.GetRequiredService<IChannel>()
                                            .CreateChannel("msgtester");
            Assert.IsNotNull(channel, "Failed to create test channel.");
            _channelId = channel.Id;
        }

        // ── Send ────────────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task SendMessage_WithValidContent_Succeeds()
        {
            var message = new Message
            {
                ChannelId = _channelId,
                SenderId = _userId,
                Content = "Hello from the in-memory test!",
            };

            using var scope = _authenticatedServices.CreateScope();
            bool result = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
                                     .SendMessage(message);

            Assert.IsTrue(result, "SendMessage should return true for a valid message.");
            Assert.AreNotEqual(Guid.Empty, message.Id, "SendMessage should assign an Id.");
        }

        [TestMethod]
        public async Task SendMessage_EmptyContent_Fails()
        {
            var message = new Message
            {
                ChannelId = _channelId,
                SenderId = _userId,
                Content = "",
            };

            using var scope = _authenticatedServices.CreateScope();
            bool result = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
                                     .SendMessage(message);

            Assert.IsFalse(result, "SendMessage should reject empty content.");
        }

        [TestMethod]
        public async Task SendMessage_NullContent_Fails()
        {
            var message = new Message
            {
                ChannelId = _channelId,
                SenderId = _userId,
                Content = null,
            };

            using var scope = _authenticatedServices.CreateScope();
            bool result = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
                                     .SendMessage(message);

            Assert.IsFalse(result, "SendMessage should reject null content.");
        }

        [TestMethod]
        public async Task SendMessage_WithRecipientContents_Succeeds()
        {
            var message = new Message
            {
                ChannelId = _channelId,
                SenderId = _userId,
                RecipientContents = new List<MessageRecipientContent>
                {
                    new()
                    {
                        UserId = _userId,
                        Content = "{\"v\":1,\"ct\":\"cipher\"}"
                    }
                }
            };

            using var scope = _authenticatedServices.CreateScope();
            bool result = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
                .SendMessage(message);

            Assert.IsTrue(result, "SendMessage should accept recipient-scoped transport payloads.");
            Assert.AreNotEqual(Guid.Empty, message.Id, "Recipient transport messages should still receive an Id.");
        }

        [TestMethod]
        public async Task SendMessage_WithAgentRecipientContents_Succeeds()
        {
            var message = new Message
            {
                ChannelId = _channelId,
                SenderId = _userId,
                AgentRecipientContents = new List<MessageAgentRecipientContent>
                {
                    new()
                    {
                        AgentDefinitionId = Guid.NewGuid(),
                        Content = "{\"v\":1,\"ct\":\"cipher\"}"
                    }
                }
            };

            using var scope = _authenticatedServices.CreateScope();
            bool result = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
                .SendMessage(message);

            Assert.IsTrue(result, "SendMessage should accept agent recipient transport payloads.");
            Assert.AreNotEqual(Guid.Empty, message.Id, "Agent recipient transport messages should still receive an Id.");
        }

        // ── Read / query ─────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task GetMessageById_AfterSend_ReturnsMessage()
        {
            // Note: Message.Content is [NotMapped], so it is checked before saving but is
            // NOT stored as a DB column. We verify the row round-trips correctly by Id.
            var message = new Message
            {
                ChannelId = _channelId,
                SenderId = _userId,
                Content = "Persisted message",
            };

            using var sendScope = _authenticatedServices.CreateScope();
            bool sent = await sendScope.ServiceProvider.GetRequiredService<IMessageRepository>()
                                       .SendMessage(message);
            Assert.IsTrue(sent, "SendMessage must succeed before we can fetch by Id.");

            using var readScope = _authenticatedServices.CreateScope();
            var fetched = await readScope.ServiceProvider.GetRequiredService<IMessageRepository>()
                                         .GetMessageById(message.Id);

            Assert.IsNotNull(fetched, "GetMessageById should return the persisted message row.");
            Assert.AreEqual(message.Id, fetched.Id);
            Assert.AreEqual(message.ChannelId, fetched.ChannelId);
            Assert.AreEqual(message.SenderId, fetched.SenderId);
        }

        [TestMethod]
        public async Task GetMessageById_NonExistentId_ReturnsNull()
        {
            using var scope = _authenticatedServices.CreateScope();
            var fetched = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
                                     .GetMessageById(Guid.NewGuid());

            Assert.IsNull(fetched, "Expected null for an unknown message Id.");
        }

        // ── Update ──────────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task UpdateMessage_ExistingMessage_Succeeds()
        {
            var original = new Message
            {
                ChannelId = _channelId,
                SenderId = _userId,
                Content = "Original content",
                ReferenceId = Guid.NewGuid(),
            };

            using var sendScope = _authenticatedServices.CreateScope();
            await sendScope.ServiceProvider.GetRequiredService<IMessageRepository>()
                           .SendMessage(original);

            var updated = new Message
            {
                Id = original.Id,
                ReferenceId = original.ReferenceId,
                Content = "Updated content",
            };

            using var updateScope = _authenticatedServices.CreateScope();
            bool result = await updateScope.ServiceProvider.GetRequiredService<IMessageRepository>()
                                           .UpdateMessage(updated);

            Assert.IsTrue(result, "UpdateMessage should return true for an existing message.");
        }

        // ── Pending messages ─────────────────────────────────────────────────────────

        [TestMethod]
        public async Task AddPendingMessage_ThenRetrieve_ReturnsPending()
        {
            // First send a real message so its Id exists in the DB.
            var message = new Message
            {
                ChannelId = _channelId,
                SenderId = _userId,
                Content = "Pending test message",
            };

            using var sendScope = _authenticatedServices.CreateScope();
            await sendScope.ServiceProvider.GetRequiredService<IMessageRepository>()
                           .SendMessage(message);

            var pending = new UserPendingMessage
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                MessageId = message.Id,
                Content = message.Content,
            };

            using var addScope = _authenticatedServices.CreateScope();
            bool added = await addScope.ServiceProvider.GetRequiredService<IMessageRepository>()
                                       .AddPendingMessage(pending);
            Assert.IsTrue(added, "AddPendingMessage should succeed.");

            using var queryScope = _authenticatedServices.CreateScope();
            var pendingList = await queryScope.ServiceProvider.GetRequiredService<IMessageRepository>()
                                              .GetPendingMessagesForUser(_userId);
            Assert.IsNotNull(pendingList);
            Assert.IsTrue(pendingList.Any(), "At least one pending message should exist.");
        }

        // ── Encryption / decryption (MessageRepository delegates to EncryptionService) ─

        [TestMethod]
        public void DecryptContent_RoundTrip_ReturnsOriginal()
        {
            // Encrypt via EncryptionService directly (same key/IV used by the repo).
            var encSvc = new Egroo.Server.Security.EncryptionService(
                TestServiceProvider.EncryptionKey,
                TestServiceProvider.EncryptionIV);

            const string plain = "Hello encryption!";
            string cipher = encSvc.Encrypt(plain);

            using var scope = _authenticatedServices.CreateScope();
            var msgRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
            string decrypted = msgRepo.DecryptContent(cipher);

            Assert.AreEqual(plain, decrypted);
        }

        [TestMethod]
        public async Task GetMessageByReferenceId_AfterSend_ReturnsMessage()
        {
            var refId = Guid.NewGuid();
            var message = new Message
            {
                ChannelId = _channelId,
                SenderId = _userId,
                Content = "Reference test",
                ReferenceId = refId,
            };

            using var sendScope = _authenticatedServices.CreateScope();
            await sendScope.ServiceProvider.GetRequiredService<IMessageRepository>()
                           .SendMessage(message);

            using var readScope = _authenticatedServices.CreateScope();
            var fetched = await readScope.ServiceProvider.GetRequiredService<IMessageRepository>()
                                         .GetMessageByReferenceId(refId);

            Assert.IsNotNull(fetched);
            Assert.AreEqual(refId, fetched.ReferenceId);
        }

        [TestMethod]
        public async Task GetMessageByReferenceId_NonExistent_ReturnsNull()
        {
            using var scope = _authenticatedServices.CreateScope();
            var fetched = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
                                     .GetMessageByReferenceId(Guid.NewGuid());

            Assert.IsNull(fetched);
        }

        [TestMethod]
        public async Task UpdatePendingMessage_RemovesPending()
        {
            var message = new Message
            {
                ChannelId = _channelId,
                SenderId = _userId,
                Content = "Pending update test",
            };

            using var sendScope = _authenticatedServices.CreateScope();
            await sendScope.ServiceProvider.GetRequiredService<IMessageRepository>()
                           .SendMessage(message);

            var pending = new UserPendingMessage
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                MessageId = message.Id,
                Content = message.Content,
            };

            using var addScope = _authenticatedServices.CreateScope();
            await addScope.ServiceProvider.GetRequiredService<IMessageRepository>()
                          .AddPendingMessage(pending);

            using var updateScope = _authenticatedServices.CreateScope();
            await updateScope.ServiceProvider.GetRequiredService<IMessageRepository>()
                             .UpdatePendingMessage(message.Id);

            using var queryScope = _authenticatedServices.CreateScope();
            var remainingPending = await queryScope.ServiceProvider.GetRequiredService<IMessageRepository>()
                                                    .GetPendingMessagesForUser(_userId);
            Assert.IsFalse(remainingPending.Any(p => p.MessageId == message.Id),
                "Pending message should be removed after UpdatePendingMessage.");
        }

        [TestMethod]
        public async Task UpdateMessage_NonExistentId_ReturnsFalse()
        {
            var updated = new Message
            {
                Id = Guid.NewGuid(),
                Content = "Won't update",
            };

            using var scope = _authenticatedServices.CreateScope();
            bool result = await scope.ServiceProvider.GetRequiredService<IMessageRepository>()
                                     .UpdateMessage(updated);

            Assert.IsFalse(result);
        }
    }
}
