using Egroo.Server.Database;
using Egroo.Server.Security;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Egroo.Server.Test
{
    [TestClass]
    public class AgentRepositoryTest
    {
        private string _dbName = null!;
        private Guid _ownerUserId;
        private Guid _otherUserId;
        private string _ownerUsername = null!;
        private string _otherUsername = null!;
        private IServiceProvider _ownerServices = null!;
        private IServiceProvider _otherServices = null!;
        private IServiceProvider _anonymousServices = null!;

        [TestInitialize]
        public async Task Initialize()
        {
            _dbName = $"AgentRepositoryTest_{Guid.NewGuid():N}";
            (_ownerUsername, _ownerUserId) = await CreateUserAndGetId("agentowner");
            (_otherUsername, _otherUserId) = await CreateUserAndGetId("agentother");

            _ownerServices = TestServiceProvider.Build(dbName: _dbName, authenticatedUserId: _ownerUserId);
            _otherServices = TestServiceProvider.Build(dbName: _dbName, authenticatedUserId: _otherUserId);
            _anonymousServices = TestServiceProvider.Build(dbName: _dbName);
        }

        [TestMethod]
        public async Task CreateAgent_WithApiKey_EncryptsAndReturnsOwnedAgent()
        {
            using var scope = _ownerServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();

            var created = await repo.CreateAgent(new AgentDefinition
            {
                Name = "helper-agent",
                Description = "covers create",
                Provider = LlmProvider.OpenAI,
                Model = "gpt-4o-mini",
                ApiKey = "plain-text-key",
                SkillsInstructionPrompt = "Skills:\n{0}"
            });

            Assert.IsNotNull(created);
            Assert.AreEqual(_ownerUserId, created.UserId);

            using var verifyScope = _ownerServices.CreateScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<DataContext>();
            var encryption = verifyScope.ServiceProvider.GetRequiredService<EncryptionService>();
            var stored = await db.AgentDefinitions.FindAsync(created.Id);

            Assert.IsNotNull(stored);
            Assert.AreNotEqual("plain-text-key", stored.ApiKey);
            Assert.AreEqual("plain-text-key", encryption.Decrypt(stored.ApiKey!));
        }

        [TestMethod]
        public async Task GetAgent_AndGetUserAgents_ReturnOnlyActiveOwnedAgents()
        {
            var active = await CreateAgentForOwner("active-agent");
            var deleted = await CreateAgentForOwner("deleted-agent");

            using (var scope = _ownerServices.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();
                var deletedResult = await repo.DeleteAgent(deleted.Id);
                Assert.IsTrue(deletedResult);
            }

            using var readScope = _ownerServices.CreateScope();
            var readRepo = readScope.ServiceProvider.GetRequiredService<IAgentRepository>();
            var fetched = await readRepo.GetAgent(active.Id);
            var agents = await readRepo.GetUserAgents();

            Assert.IsNotNull(fetched);
            Assert.IsNotNull(agents);
            Assert.AreEqual(1, agents.Length);
            Assert.AreEqual(active.Id, agents[0].Id);
        }

        [TestMethod]
        public async Task UpdateAgent_WithoutNewApiKey_PreservesExistingEncryptedKey()
        {
            var created = await CreateAgentForOwner("update-agent", apiKey: "initial-key");

            using var scope = _ownerServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var encryption = scope.ServiceProvider.GetRequiredService<EncryptionService>();
            var before = await db.AgentDefinitions.FindAsync(created.Id);

            var success = await repo.UpdateAgent(new AgentDefinition
            {
                Id = created.Id,
                Name = "updated-name",
                Description = "updated-description",
                Instructions = "updated-instructions",
                Provider = LlmProvider.Ollama,
                Model = "llama3.2",
                Endpoint = "http://localhost:11434",
                IsActive = false,
                IsPublished = true,
                AddPermission = AgentAddPermission.OwnerOnly,
                Temperature = 0.4f,
                MaxTokens = 256,
                SkillsInstructionPrompt = "Updated:\n{0}",
                ApiKey = string.Empty
            });

            var after = await db.AgentDefinitions.FindAsync(created.Id);

            Assert.IsTrue(success);
            Assert.IsNotNull(before);
            Assert.IsNotNull(after);
            Assert.AreEqual(before.ApiKey, after.ApiKey);
            Assert.AreEqual("initial-key", encryption.Decrypt(after.ApiKey!));
            Assert.AreEqual("updated-name", after.Name);
            Assert.IsTrue(after.IsPublished);
            Assert.AreEqual(AgentAddPermission.OwnerOnly, after.AddPermission);
        }

        [TestMethod]
        public async Task KnowledgeLifecycle_CanAddReadUpdateAndDelete()
        {
            var agent = await CreateAgentForOwner("knowledge-agent");

            using var scope = _ownerServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();

            var created = await repo.AddKnowledge(new AgentKnowledge
            {
                AgentDefinitionId = agent.Id,
                Title = "faq",
                Content = "Initial content",
                IsEnabled = true
            });

            Assert.IsNotNull(created);

            var listed = await repo.GetAgentKnowledge(agent.Id);
            Assert.IsNotNull(listed);
            Assert.AreEqual(1, listed.Length);

            created.Title = "faq-updated";
            created.Content = "Updated content";
            created.IsEnabled = false;

            var updated = await repo.UpdateKnowledge(created);
            var deleted = await repo.DeleteKnowledge(created.Id);
            var afterDelete = await repo.GetAgentKnowledge(agent.Id);

            Assert.IsTrue(updated);
            Assert.IsTrue(deleted);
            Assert.IsNotNull(afterDelete);
            Assert.AreEqual(0, afterDelete.Length);
        }

        [TestMethod]
        public async Task ToolLifecycle_CanAddReadUpdateAndDelete()
        {
            var agent = await CreateAgentForOwner("tool-agent");

            using var scope = _ownerServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();

            var created = await repo.AddTool(new AgentTool
            {
                AgentDefinitionId = agent.Id,
                Name = "weather_lookup",
                Description = "Finds weather",
                ParametersSchema = "{\"type\":\"object\"}",
                IsEnabled = true
            });

            Assert.IsNotNull(created);

            created.Name = "weather_lookup_v2";
            created.Description = "Finds weather with units";
            created.ParametersSchema = "{\"type\":\"object\",\"properties\":{}}";
            created.IsEnabled = false;

            var updated = await repo.UpdateTool(created);
            var tools = await repo.GetAgentTools(agent.Id);
            var deleted = await repo.DeleteTool(created.Id);
            var afterDelete = await repo.GetAgentTools(agent.Id);

            Assert.IsTrue(updated);
            Assert.IsNotNull(tools);
            Assert.AreEqual("weather_lookup_v2", tools[0].Name);
            Assert.IsTrue(deleted);
            Assert.IsNotNull(afterDelete);
            Assert.AreEqual(0, afterDelete.Length);
        }

        [TestMethod]
        public async Task SkillDirectoryLifecycle_CanAddReadUpdateAndDelete()
        {
            var agent = await CreateAgentForOwner("skills-agent");

            using var scope = _ownerServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();

            var created = await repo.AddSkillDirectory(new AgentSkillDirectory
            {
                AgentDefinitionId = agent.Id,
                Name = "Team A",
                Path = "skills/team-a",
                IsEnabled = true
            });

            Assert.IsNotNull(created);

            var fetched = await repo.GetSkillDirectory(created.Id);
            Assert.IsNotNull(fetched);
            Assert.AreEqual("Team A", fetched.Name);

            created.Name = "Team B";
            created.Path = "skills/team-b";
            created.IsEnabled = false;

            var updated = await repo.UpdateSkillDirectory(created);
            var listed = await repo.GetAgentSkillDirectories(agent.Id);
            var deleted = await repo.DeleteSkillDirectory(created.Id);
            var afterDelete = await repo.GetAgentSkillDirectories(agent.Id);

            Assert.IsTrue(updated);
            Assert.IsNotNull(listed);
            Assert.AreEqual("Team B", listed[0].Name);
            Assert.AreEqual("skills/team-b", listed[0].Path);
            Assert.IsTrue(deleted);
            Assert.IsNotNull(afterDelete);
            Assert.AreEqual(0, afterDelete.Length);
        }

        [TestMethod]
        public async Task DeleteToolsByMcpServer_SoftDeletesAllMatchingTools()
        {
            var agent = await CreateAgentForOwner("tool-delete-agent");

            using var scope = _ownerServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();
            var server = await repo.AddMcpServer(new AgentMcpServer
            {
                AgentDefinitionId = agent.Id,
                Name = "Docs MCP",
                Endpoint = "https://example.com/mcp",
                ApiKey = "server-secret"
            });

            Assert.IsNotNull(server);

            await repo.AddTool(new AgentTool
            {
                AgentDefinitionId = agent.Id,
                Name = "docs_search",
                Description = "Search docs",
                McpServerId = server.Id,
                Source = AgentToolSource.Mcp
            });
            await repo.AddTool(new AgentTool
            {
                AgentDefinitionId = agent.Id,
                Name = "docs_read",
                Description = "Read docs",
                McpServerId = server.Id,
                Source = AgentToolSource.Mcp
            });

            var deleted = await repo.DeleteToolsByMcpServer(server.Id);
            var remaining = await repo.GetAgentTools(agent.Id);

            Assert.IsTrue(deleted);
            Assert.IsNotNull(remaining);
            Assert.AreEqual(0, remaining.Length);
        }

        [TestMethod]
        public async Task McpServerLifecycle_EncryptsKeyAndSoftDeletesAssociatedTools()
        {
            var agent = await CreateAgentForOwner("mcp-agent");

            using var scope = _ownerServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var encryption = scope.ServiceProvider.GetRequiredService<EncryptionService>();

            var server = await repo.AddMcpServer(new AgentMcpServer
            {
                AgentDefinitionId = agent.Id,
                Name = "MCP One",
                Endpoint = "https://example.com/sse",
                ApiKey = "mcp-key",
                IsActive = true
            });

            Assert.IsNotNull(server);
            Assert.AreEqual("mcp-key", encryption.Decrypt(server.ApiKey!));

            await repo.AddTool(new AgentTool
            {
                AgentDefinitionId = agent.Id,
                Name = "remote_tool",
                Description = "Uses mcp",
                McpServerId = server.Id,
                Source = AgentToolSource.Mcp
            });

            var updateResult = await repo.UpdateMcpServer(new AgentMcpServer
            {
                Id = server.Id,
                Name = "MCP Renamed",
                Endpoint = "https://example.com/http",
                IsActive = false,
                LastDiscoveredAt = DateTimeOffset.UtcNow,
                ApiKey = string.Empty
            });
            var fetched = await repo.GetMcpServer(server.Id);
            var listed = await repo.GetAgentMcpServers(agent.Id);
            var deleted = await repo.DeleteMcpServer(server.Id);
            var tool = await db.AgentTools.FirstOrDefaultAsync(x => x.McpServerId == server.Id);

            Assert.IsTrue(updateResult);
            Assert.IsNotNull(fetched);
            Assert.AreEqual("MCP Renamed", fetched.Name);
            Assert.AreEqual("mcp-key", encryption.Decrypt(fetched.ApiKey!));
            Assert.IsNotNull(listed);
            Assert.AreEqual(1, listed.Length);
            Assert.IsTrue(deleted);
            Assert.IsNotNull(tool);
            Assert.IsNotNull(tool.DateDeleted);
        }

        [TestMethod]
        public async Task ConversationAndMessagesLifecycle_CanRoundTripWithPagination()
        {
            var agent = await CreateAgentForOwner("conversation-agent");

            using var scope = _ownerServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();

            var conversation = await repo.CreateConversation(agent.Id, "Support chat");
            Assert.IsNotNull(conversation);

            var first = await repo.AddMessage(new AgentConversationMessage
            {
                AgentConversationId = conversation.Id,
                Role = "user",
                Content = "Hello"
            });
            var second = await repo.AddMessage(new AgentConversationMessage
            {
                AgentConversationId = conversation.Id,
                Role = "assistant",
                Content = "Hi there"
            });

            var fetched = await repo.GetConversation(conversation.Id);
            var conversations = await repo.GetUserConversations(agent.Id);
            var paged = await repo.GetConversationMessages(conversation.Id, skip: 1, take: 2);
            var updated = await repo.UpdateConversationSessionState(conversation.Id, "{\"turn\":2}");
            var deleted = await repo.DeleteConversation(conversation.Id);
            var afterDelete = await repo.GetConversation(conversation.Id);

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.IsNotNull(fetched);
            Assert.IsNotNull(conversations);
            Assert.AreEqual(1, conversations.Length);
            Assert.IsNotNull(paged);
            Assert.AreEqual(1, paged.Length);
            Assert.AreEqual(second.Id, paged[0].Id);
            Assert.IsTrue(updated);
            Assert.IsTrue(deleted);
            Assert.IsNull(afterDelete);
        }

        [TestMethod]
        public async Task InternalHelpers_CanDecryptAgentKey_AndAddMessage()
        {
            var agent = await CreateAgentForOwner("internal-agent", apiKey: "super-secret");

            using var scope = _ownerServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();
            var conversation = await repo.CreateConversation(agent.Id, "Internal helpers");

            Assert.IsNotNull(conversation);

            var concreteType = repo.GetType();
            var getAgentMethod = concreteType.GetMethod("GetAgentWithDecryptedKey", BindingFlags.Instance | BindingFlags.NonPublic);
            var addMessageMethod = concreteType.GetMethod("AddMessageInternal", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(getAgentMethod);
            Assert.IsNotNull(addMessageMethod);

            var getAgentTask = (Task)getAgentMethod!.Invoke(repo, new object[] { agent.Id, _ownerUserId })!;
            await getAgentTask;
            var getAgentResult = getAgentTask.GetType().GetProperty("Result")!.GetValue(getAgentTask);

            Assert.IsNotNull(getAgentResult);
            Assert.AreEqual("super-secret", getAgentResult.GetType().GetField("Item2")!.GetValue(getAgentResult));

            var addMessageTask = (Task)addMessageMethod!.Invoke(repo, new object[]
            {
                new AgentConversationMessage
                {
                    AgentConversationId = conversation.Id,
                    Role = "system",
                    Content = "Injected"
                }
            })!;
            await addMessageTask;
            var addedMessage = addMessageTask.GetType().GetProperty("Result")!.GetValue(addMessageTask) as AgentConversationMessage;
            var messages = await repo.GetConversationMessages(conversation.Id);

            Assert.IsNotNull(addedMessage);
            Assert.IsNotNull(messages);
            Assert.AreEqual(1, messages.Length);
            Assert.AreEqual("Injected", messages[0].Content);
        }

        [TestMethod]
        public async Task PublishAndSearchAgents_RespectSharingPermissions()
        {
            var sharedAgent = await CreateAgentForOwner("shared-search-agent", isPublished: true, addPermission: AgentAddPermission.OwnerAndOthers);
            var privateSharedAgent = await CreateAgentForOwner("owner-only-agent", isPublished: true, addPermission: AgentAddPermission.OwnerOnly);

            using var otherScope = _otherServices.CreateScope();
            var otherRepo = otherScope.ServiceProvider.GetRequiredService<IAgentRepository>();
            var otherVisibleAgents = await otherRepo.SearchPublishedAgents("agent");
            var visibleShared = await otherRepo.GetPublishedAgent(sharedAgent.Id);
            var hiddenOwnerOnly = await otherRepo.GetPublishedAgent(privateSharedAgent.Id);

            using var ownerScope = _ownerServices.CreateScope();
            var ownerRepo = ownerScope.ServiceProvider.GetRequiredService<IAgentRepository>();
            var publishToggle = await ownerRepo.PublishAgent(sharedAgent.Id, false);
            var ownerVisibleAgents = await ownerRepo.SearchPublishedAgents("agent");

            Assert.IsNotNull(otherVisibleAgents);
            Assert.AreEqual(1, otherVisibleAgents.Length);
            Assert.AreEqual(sharedAgent.Id, otherVisibleAgents[0].Id);
            Assert.IsNotNull(visibleShared);
            Assert.IsNull(hiddenOwnerOnly);
            Assert.IsTrue(publishToggle);
            Assert.IsNotNull(ownerVisibleAgents);
            Assert.AreEqual(1, ownerVisibleAgents.Length);
            Assert.AreEqual(privateSharedAgent.Id, ownerVisibleAgents[0].Id);
        }

        [TestMethod]
        public async Task SearchPublishedAgents_WithEmptyQuery_ReturnsEmptyArray()
        {
            using var scope = _otherServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();

            var result = await repo.SearchPublishedAgents("   ");

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public async Task AgentFriendLifecycle_RespectsPermissionAndSupportsRemoval()
        {
            var sharedAgent = await CreateAgentForOwner("friend-agent", isPublished: true, addPermission: AgentAddPermission.OwnerAndOthers);
            var ownerOnlyAgent = await CreateAgentForOwner("owner-only-friend-agent", isPublished: true, addPermission: AgentAddPermission.OwnerOnly);

            using var otherScope = _otherServices.CreateScope();
            var repo = otherScope.ServiceProvider.GetRequiredService<IAgentRepository>();

            var denied = await repo.AddAgentFriend(ownerOnlyAgent.Id);
            var added = await repo.AddAgentFriend(sharedAgent.Id);
            var addedAgain = await repo.AddAgentFriend(sharedAgent.Id);
            var isFriend = await repo.IsAgentFriend(sharedAgent.Id);
            var friends = await repo.GetUserAgentFriends();
            var removed = await repo.RemoveAgentFriend(sharedAgent.Id);
            var isFriendAfterRemoval = await repo.IsAgentFriend(sharedAgent.Id);

            Assert.IsFalse(denied);
            Assert.IsTrue(added);
            Assert.IsTrue(addedAgain);
            Assert.IsTrue(isFriend);
            Assert.IsNotNull(friends);
            Assert.AreEqual(1, friends.Length);
            Assert.IsTrue(removed);
            Assert.IsFalse(isFriendAfterRemoval);
        }

        [TestMethod]
        public async Task AddAgentToChannel_ForOwnedAgent_SucceedsWithoutFriendship()
        {
            var agent = await CreateAgentForOwner("channel-owned-agent");
            var channel = await CreateAdminChannel(_ownerUserId);

            using var scope = _ownerServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();

            var added = await repo.AddAgentToChannel(channel.Id, agent.Id);
            var addedAgain = await repo.AddAgentToChannel(channel.Id, agent.Id);
            var channelAgents = await repo.GetChannelAgents(channel.Id);
            var definitions = await repo.GetChannelAgentDefinitions(channel.Id);
            var removed = await repo.RemoveAgentFromChannel(channel.Id, agent.Id);
            var definitionsAfterRemoval = await repo.GetChannelAgentDefinitions(channel.Id);

            Assert.IsTrue(added);
            Assert.IsTrue(addedAgain);
            Assert.IsNotNull(channelAgents);
            Assert.AreEqual(1, channelAgents.Length);
            Assert.IsNotNull(definitions);
            Assert.AreEqual(1, definitions.Length);
            Assert.IsTrue(removed);
            Assert.IsNotNull(definitionsAfterRemoval);
            Assert.AreEqual(0, definitionsAfterRemoval.Length);
        }

        [TestMethod]
        public async Task AddAgentToChannel_ForSharedForeignAgent_RequiresFriendship()
        {
            var foreignAgent = await CreateAgentForOwner("shared-channel-agent", isPublished: true, addPermission: AgentAddPermission.OwnerAndOthers);
            var otherChannel = await CreateAdminChannel(_otherUserId);

            using var otherScope = _otherServices.CreateScope();
            var repo = otherScope.ServiceProvider.GetRequiredService<IAgentRepository>();

            var deniedBeforeFriend = await repo.AddAgentToChannel(otherChannel.Id, foreignAgent.Id);
            await SeedFriendship(_otherUserId, foreignAgent.Id);
            var addedAfterFriend = await repo.AddAgentToChannel(otherChannel.Id, foreignAgent.Id);

            Assert.IsFalse(deniedBeforeFriend);
            Assert.IsTrue(addedAfterFriend);
        }

        [TestMethod]
        public async Task AddAgentToChannel_ForOwnerOnlyForeignAgent_IsRejected()
        {
            var foreignAgent = await CreateAgentForOwner("owner-only-channel-agent", isPublished: true, addPermission: AgentAddPermission.OwnerOnly);
            var otherChannel = await CreateAdminChannel(_otherUserId);

            using var otherScope = _otherServices.CreateScope();
            var repo = otherScope.ServiceProvider.GetRequiredService<IAgentRepository>();

            var added = await repo.AddAgentToChannel(otherChannel.Id, foreignAgent.Id);

            Assert.IsFalse(added);
        }

        [TestMethod]
        public async Task AnonymousCalls_ReturnNullOrFalseForProtectedOperations()
        {
            using var scope = _anonymousServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();

            var created = await repo.CreateAgent(new AgentDefinition
            {
                Name = "anon-agent",
                Provider = LlmProvider.OpenAI,
                Model = "gpt-4o-mini"
            });
            var knowledge = await repo.AddKnowledge(new AgentKnowledge
            {
                AgentDefinitionId = Guid.NewGuid(),
                Title = "anon",
                Content = "anon"
            });
            var updated = await repo.PublishAgent(Guid.NewGuid(), true);
            var friends = await repo.GetUserAgentFriends();

            Assert.IsNull(created);
            Assert.IsNull(knowledge);
            Assert.IsFalse(updated);
            Assert.IsNull(friends);
        }

        [TestMethod]
        public async Task OwnershipChecks_BlockMutationsAgainstAnotherUsersAgent()
        {
            var ownerAgent = await CreateAgentForOwner("ownership-agent");

            using var otherScope = _otherServices.CreateScope();
            var otherRepo = otherScope.ServiceProvider.GetRequiredService<IAgentRepository>();

            var addKnowledge = await otherRepo.AddKnowledge(new AgentKnowledge
            {
                AgentDefinitionId = ownerAgent.Id,
                Title = "blocked",
                Content = "blocked"
            });
            var addTool = await otherRepo.AddTool(new AgentTool
            {
                AgentDefinitionId = ownerAgent.Id,
                Name = "blocked_tool",
                Description = "blocked"
            });
            var addMcp = await otherRepo.AddMcpServer(new AgentMcpServer
            {
                AgentDefinitionId = ownerAgent.Id,
                Name = "blocked server",
                Endpoint = "https://example.com"
            });
            var createConversation = await otherRepo.CreateConversation(ownerAgent.Id, "blocked");

            Assert.IsNull(addKnowledge);
            Assert.IsNull(addTool);
            Assert.IsNull(addMcp);
            Assert.IsNull(createConversation);
        }

        private async Task<(string username, Guid userId)> CreateUserAndGetId(string usernameBase)
        {
            var username = $"{usernameBase}{Guid.NewGuid():N}"[..16];
            var services = TestServiceProvider.Build(dbName: _dbName);

            using var signUpScope = services.CreateScope();
            var auth = signUpScope.ServiceProvider.GetRequiredService<IAuth>();
            var signUpResult = await auth.SignUp(username, "ValidP@ss1!");
            Assert.IsTrue(signUpResult.Success, $"Sign-up failed for {username}: {signUpResult.Message}");

            using var signInScope = services.CreateScope();
            var signInResult = await signInScope.ServiceProvider.GetRequiredService<IAuth>().SignIn(username, "ValidP@ss1!");
            Assert.IsTrue(signInResult.Success, $"Sign-in failed for {username}: {signInResult.Message}");

            return (username, signInResult.UserId!.Value);
        }

        private async Task<AgentDefinition> CreateAgentForOwner(
            string name,
            string? apiKey = "owner-api-key",
            bool isPublished = false,
            AgentAddPermission addPermission = AgentAddPermission.OwnerAndOthers)
        {
            using var scope = _ownerServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var encryption = scope.ServiceProvider.GetRequiredService<EncryptionService>();

            var created = new AgentDefinition
            {
                Id = Guid.NewGuid(),
                UserId = _ownerUserId,
                Name = name,
                Description = $"Description for {name}",
                Instructions = $"Instructions for {name}",
                Provider = LlmProvider.OpenAI,
                Model = "gpt-4o-mini",
                ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : encryption.Encrypt(apiKey),
                IsPublished = isPublished,
                AddPermission = addPermission,
                IsActive = true,
                Temperature = 0.2f,
                MaxTokens = 128,
                SkillsInstructionPrompt = "Available skills:\n{0}",
                DateCreated = DateTimeOffset.UtcNow,
                CreatedBy = _ownerUserId
            };

            await db.AgentDefinitions.AddAsync(created);
            await db.SaveChangesAsync();

            return created;
        }

        private async Task<Channel> CreateAdminChannel(Guid userId)
        {
            using var scope = _ownerServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            var channel = new Channel
            {
                Id = Guid.NewGuid(),
                DateCreated = DateTimeOffset.UtcNow,
                CreatedBy = userId
            };

            var channelUser = new ChannelUser
            {
                Id = Guid.NewGuid(),
                ChannelId = channel.Id,
                UserId = userId,
                IsAdmin = true,
                DateCreated = DateTimeOffset.UtcNow,
                CreatedBy = userId
            };

            await db.Channels.AddAsync(channel);
            await db.ChannelUsers.AddAsync(channelUser);
            await db.SaveChangesAsync();

            return channel;
        }

        private async Task SeedFriendship(Guid userId, Guid agentId)
        {
            using var scope = _ownerServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            var existing = await db.UserAgentFriends.FirstOrDefaultAsync(x => x.UserId == userId && x.AgentDefinitionId == agentId && x.DateDeleted == null);
            if (existing is not null)
            {
                return;
            }

            await db.UserAgentFriends.AddAsync(new UserAgentFriend
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AgentDefinitionId = agentId,
                DateCreated = DateTimeOffset.UtcNow,
                CreatedBy = userId
            });
            await db.SaveChangesAsync();
        }

        private static async Task<Channel> CreateChannelForUser(IServiceProvider services, string username)
        {
            using var scope = services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IChannel>();
            var channel = await repo.CreateChannel(username);
            Assert.IsNotNull(channel);
            return channel;
        }
    }
}