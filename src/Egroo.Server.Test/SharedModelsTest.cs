namespace Egroo.Server.Test
{
    [TestClass]
    public class SharedModelsTest
    {
        // ── Channel ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Channel_GetTitle_PrefersExplicitTitle()
        {
            var channel = new Channel { Title = "General", DefaultTitle = "Fallback" };
            Assert.AreEqual("General", channel.GetTitle());
        }

        [TestMethod]
        public void Channel_GetTitle_FallsBackToDefaultTitle()
        {
            var channel = new Channel { Title = "  ", DefaultTitle = "Fallback" };
            Assert.AreEqual("Fallback", channel.GetTitle());
        }

        [TestMethod]
        public void Channel_GetTitle_NullTitle_FallsBackToDefaultTitle()
        {
            var channel = new Channel { Title = null, DefaultTitle = "Default" };
            Assert.AreEqual("Default", channel.GetTitle());
        }

        [TestMethod]
        public void Channel_GetTitle_BothNull_ReturnsNull()
        {
            var channel = new Channel { Title = null, DefaultTitle = null };
            Assert.IsNull(channel.GetTitle());
        }

        [TestMethod]
        public void Channel_DefaultProperties()
        {
            var channel = new Channel();
            Assert.IsFalse(channel.IsPublic);
            Assert.IsNull(channel.Title);
            Assert.IsNull(channel.DefaultTitle);
            Assert.AreNotEqual(Guid.Empty, channel.Id);
        }

        // ── UserDetail ──────────────────────────────────────────────────────────────

        [TestMethod]
        public void UserDetail_Helpers_ReturnExpectedValues()
        {
            var detail = new UserDetail
            {
                FirstName = "Repo",
                LastName = "Owner",
                DisplayName = "Display",
                Region = "Amman",
                Country = "Jordan",
                PhoneCountryCode = "962",
                PhoneNumber = "123456",
                Interests = "code,chat,test",
                Sex = UserDetail.SexEnum.Other
            };

            CollectionAssert.AreEqual(new[] { "code", "chat", "test" }, detail.GetInterests());
            Assert.AreEqual("Repo Owner", detail.GetFullName());
            Assert.AreEqual("Display", detail.GetDisplayName());
            Assert.AreEqual("Amman, Jordan", detail.GetComposedAddress());
            Assert.AreEqual("+962 123456", detail.GetComposedPhone());
            Assert.AreEqual("Other", detail.GetSex());
        }

        [TestMethod]
        public void UserDetail_GetDisplayName_FallsToFullName_WhenDisplayNameEmpty()
        {
            var detail = new UserDetail
            {
                FirstName = "John",
                LastName = "Doe",
                DisplayName = null
            };

            Assert.AreEqual("John Doe", detail.GetDisplayName());
        }

        [TestMethod]
        public void UserDetail_GetDisplayName_FallsToFullName_WhenWhitespace()
        {
            var detail = new UserDetail
            {
                FirstName = "Jane",
                LastName = "Smith",
                DisplayName = "  "
            };

            Assert.AreEqual("Jane Smith", detail.GetDisplayName());
        }

        [TestMethod]
        public void UserDetail_GetInterests_NullInterests_ReturnsEmpty()
        {
            var detail = new UserDetail { Interests = null };
            Assert.AreEqual(0, detail.GetInterests().Count);
        }

        [TestMethod]
        public void UserDetail_SexEnum_AllValues()
        {
            Assert.AreEqual("NotSpecified", new UserDetail { Sex = UserDetail.SexEnum.NotSpecified }.GetSex());
            Assert.AreEqual("Male", new UserDetail { Sex = UserDetail.SexEnum.Male }.GetSex());
            Assert.AreEqual("Female", new UserDetail { Sex = UserDetail.SexEnum.Female }.GetSex());
            Assert.AreEqual("Other", new UserDetail { Sex = UserDetail.SexEnum.Other }.GetSex());
        }

        [TestMethod]
        public void UserDetail_DefaultSex_IsNotSpecified()
        {
            var detail = new UserDetail();
            Assert.AreEqual(UserDetail.SexEnum.NotSpecified, detail.Sex);
        }

        // ── UserDto ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void UserDto_HelperMethods_ReturnCopiesAndPreview()
        {
            var user = new UserDto
            {
                UserDetail = new UserDetail
                {
                    DisplayName = "Display",
                    FirstName = "First",
                    LastName = "Last",
                    Email = "mail@example.com",
                    PhoneNumber = "123",
                    PhoneCountryCode = "1",
                    Region = "North",
                    Country = "Earth"
                },
                UserStorage = new UserStorage
                {
                    AvatarImageBase64 = "avatar",
                    CoverImageBase64 = "cover"
                }
            };

            Assert.AreEqual("Display", user.GetPublicDetail()?.DisplayName);
            Assert.AreEqual("First", user.GetPrivateDetail()?.FirstName);
            Assert.AreEqual("cover", user.GetStorage()?.CoverImageBase64);
            Assert.AreEqual("avatar", user.GetAvatar()?.AvatarImageBase64);
            Assert.AreEqual("cover", user.GetCover()?.CoverImageBase64);
            Assert.AreEqual("data:image/png;base64,abc123", user.CombineAvatarForPreview(new MediaResult("PNG", "abc123")));
            Assert.IsNull(user.CombineAvatarForPreview(new MediaResult("", "abc123")));
        }

        [TestMethod]
        public void UserDto_GetPublicDetail_OnlyIncludesDisplayName()
        {
            var user = new UserDto
            {
                UserDetail = new UserDetail
                {
                    DisplayName = "Public",
                    FirstName = "Private",
                    Email = "private@email.com"
                }
            };

            var publicDetail = user.GetPublicDetail();
            Assert.IsNotNull(publicDetail);
            Assert.AreEqual("Public", publicDetail.DisplayName);
            Assert.IsNull(publicDetail.Email);
            Assert.IsNull(publicDetail.FirstName);
        }

        [TestMethod]
        public void UserDto_GetPrivateDetail_IncludesAllFields()
        {
            var user = new UserDto
            {
                UserDetail = new UserDetail
                {
                    DisplayName = "Display",
                    FirstName = "First",
                    LastName = "Last",
                    Email = "email@test.com",
                    PhoneNumber = "123",
                    PhoneCountryCode = "1",
                    Region = "West",
                    Country = "US"
                }
            };

            var privateDetail = user.GetPrivateDetail();
            Assert.IsNotNull(privateDetail);
            Assert.AreEqual("Display", privateDetail.DisplayName);
            Assert.AreEqual("First", privateDetail.FirstName);
            Assert.AreEqual("Last", privateDetail.LastName);
            Assert.AreEqual("email@test.com", privateDetail.Email);
            Assert.AreEqual("123", privateDetail.PhoneNumber);
            Assert.AreEqual("1", privateDetail.PhoneCountryCode);
            Assert.AreEqual("West", privateDetail.Region);
            Assert.AreEqual("US", privateDetail.Country);
        }

        [TestMethod]
        public void UserDto_CombineAvatarForPreview_NullResult_ReturnsNull()
        {
            var user = new UserDto();
            Assert.IsNull(user.CombineAvatarForPreview(null));
        }

        [TestMethod]
        public void UserDto_CombineAvatarForPreview_EmptyImageBase64_ReturnsNull()
        {
            var user = new UserDto();
            Assert.IsNull(user.CombineAvatarForPreview(new MediaResult("png", "")));
        }

        [TestMethod]
        public void UserDto_NullUserDetail_ReturnsDefaults()
        {
            var user = new UserDto { UserDetail = null };
            var publicDetail = user.GetPublicDetail();
            Assert.IsNotNull(publicDetail);
            Assert.IsNull(publicDetail.DisplayName);
        }

        [TestMethod]
        public void UserDto_NullUserStorage_ReturnsDefaultStorage()
        {
            var user = new UserDto { UserStorage = null };
            var storage = user.GetStorage();
            Assert.IsNotNull(storage);
            Assert.IsNull(storage.AvatarImageBase64);
        }

        [TestMethod]
        public void UserDto_DefaultProperties()
        {
            var user = new UserDto();
            Assert.IsFalse(user.IsOnline);
            Assert.IsFalse(user.InCall);
            Assert.IsNull(user.AvatarPreview);
            Assert.IsNull(user.ConnectionId);
            Assert.IsNotNull(user.UserFeedbacks);
            Assert.AreEqual(0, user.UserFeedbacks.Count);
        }

        // ── EntityBase / EntityAudit ────────────────────────────────────────────────

        [TestMethod]
        public void EntityBase_DefaultId_IsNotEmpty()
        {
            var channel = new Channel();
            Assert.AreNotEqual(Guid.Empty, channel.Id);
        }

        [TestMethod]
        public void EntityAudit_DefaultDateCreated_IsNotNull()
        {
            var channel = new Channel();
            Assert.IsNotNull(channel.DateCreated);
        }

        // ── AgentDefinition model ──────────────────────────────────────────────────

        [TestMethod]
        public void AgentDefinition_DefaultProperties()
        {
            var agent = new AgentDefinition();
            Assert.IsTrue(agent.IsActive);
            Assert.IsFalse(agent.IsPublished);
            Assert.AreEqual(AgentAddPermission.OwnerAndOthers, agent.AddPermission);
            Assert.AreEqual(string.Empty, agent.Name);
            Assert.AreEqual(string.Empty, agent.Model);
        }

        [TestMethod]
        public void AgentTool_DefaultProperties()
        {
            var tool = new AgentTool();
            Assert.IsTrue(tool.IsEnabled);
            Assert.AreEqual(AgentToolSource.Builtin, tool.Source);
            Assert.IsNull(tool.McpServerId);
        }

        [TestMethod]
        public void AgentKnowledge_DefaultProperties()
        {
            var knowledge = new AgentKnowledge();
            Assert.IsTrue(knowledge.IsEnabled);
            Assert.AreEqual(string.Empty, knowledge.Title);
            Assert.AreEqual(string.Empty, knowledge.Content);
        }

        [TestMethod]
        public void AgentMcpServer_DefaultProperties()
        {
            var server = new AgentMcpServer();
            Assert.IsTrue(server.IsActive);
            Assert.AreEqual(string.Empty, server.Name);
            Assert.AreEqual(string.Empty, server.Endpoint);
        }

        [TestMethod]
        public void AgentSkillDirectory_DefaultProperties()
        {
            var dir = new AgentSkillDirectory();
            Assert.IsTrue(dir.IsEnabled);
            Assert.AreEqual(string.Empty, dir.Name);
            Assert.AreEqual(string.Empty, dir.Path);
        }

        [TestMethod]
        public void LlmProvider_Enum_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)LlmProvider.OpenAI);
            Assert.AreEqual(1, (int)LlmProvider.AzureOpenAI);
            Assert.AreEqual(2, (int)LlmProvider.Anthropic);
            Assert.AreEqual(3, (int)LlmProvider.Ollama);
        }

        [TestMethod]
        public void AgentAddPermission_Enum_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)AgentAddPermission.OwnerOnly);
            Assert.AreEqual(1, (int)AgentAddPermission.OwnerAndOthers);
        }

        // ── EntityChildBase ─────────────────────────────────────────────────────────

        [TestMethod]
        public void EntityBase_IdProperty_DefaultsToNewGuid()
        {
            var entity = new AgentPendingMessage();
            Assert.AreNotEqual(Guid.Empty, entity.Id);
        }

        // ── AgentPendingMessage ─────────────────────────────────────────────────────

        [TestMethod]
        public void AgentPendingMessage_DefaultProperties()
        {
            var pm = new AgentPendingMessage();
            Assert.AreEqual(Guid.Empty, pm.AgentDefinitionId);
            Assert.AreEqual(Guid.Empty, pm.MessageId);
            Assert.IsNull(pm.Content);
        }

        [TestMethod]
        public void AgentPendingMessage_SetProperties()
        {
            var defId = Guid.NewGuid();
            var msgId = Guid.NewGuid();
            var pm = new AgentPendingMessage
            {
                AgentDefinitionId = defId,
                MessageId = msgId,
                Content = "test content",
            };
            Assert.AreEqual(defId, pm.AgentDefinitionId);
            Assert.AreEqual(msgId, pm.MessageId);
            Assert.AreEqual("test content", pm.Content);
        }

        // ── AgentConversationMessage ────────────────────────────────────────────────

        [TestMethod]
        public void AgentConversationMessage_SetAllProperties()
        {
            var msg = new AgentConversationMessage
            {
                AgentConversationId = Guid.NewGuid(),
                Role = "user",
                Content = "Hello",
                ToolCallId = "call_123",
                ToolName = "web_search",
                TokensUsed = 42,
            };
            Assert.AreNotEqual(Guid.Empty, msg.AgentConversationId);
            Assert.AreEqual("user", msg.Role);
            Assert.AreEqual("Hello", msg.Content);
            Assert.AreEqual("call_123", msg.ToolCallId);
            Assert.AreEqual("web_search", msg.ToolName);
            Assert.AreEqual(42, msg.TokensUsed);
        }

        // ── ChannelTypingState ──────────────────────────────────────────────────────

        [TestMethod]
        public void ChannelTypingState_SetAllProperties()
        {
            var state = new ChannelTypingState
            {
                ChannelId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                DisplayName = "testuser",
                IsAgent = false,
            };
            Assert.IsFalse(state.IsAgent);
            Assert.AreEqual("testuser", state.DisplayName);
        }

        // ── UserPendingMessage ──────────────────────────────────────────────────────

        [TestMethod]
        public void UserPendingMessage_SetAllProperties()
        {
            var pm = new UserPendingMessage
            {
                UserId = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                Content = "encrypted",
            };
            Assert.IsNotNull(pm.UserId);
            Assert.IsNotNull(pm.MessageId);
            Assert.AreEqual("encrypted", pm.Content);
        }

        // ── UserDetail ──────────────────────────────────────────────────────────────

        [TestMethod]
        public void UserDetail_SetAllProperties()
        {
            var detail = new UserDetail
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john@example.com",
                DisplayName = "JohnD",
                Region = "NA",
                Country = "US",
            };
            Assert.AreEqual("John", detail.FirstName);
            Assert.AreEqual("Doe", detail.LastName);
            Assert.AreEqual("john@example.com", detail.Email);
            Assert.AreEqual("JohnD", detail.DisplayName);
            Assert.AreEqual("NA", detail.Region);
            Assert.AreEqual("US", detail.Country);
        }

        // ── Message ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Message_SetAgentRecipientContents()
        {
            var msg = new Message
            {
                AgentRecipientContents = new List<MessageAgentRecipientContent>
                {
                    new() { AgentDefinitionId = Guid.NewGuid(), Content = "data" }
                }
            };
            Assert.AreEqual(1, msg.AgentRecipientContents.Count);
        }

        // ── EntityAudit ─────────────────────────────────────────────────────────────

        [TestMethod]
        public void EntityAudit_SoftDeleteProperties()
        {
            var msg = new Message
            {
                DateDeleted = DateTimeOffset.UtcNow,
                DeletedBy = Guid.NewGuid(),
            };
            Assert.IsNotNull(msg.DateDeleted);
            Assert.IsNotNull(msg.DeletedBy);
        }
    }
}