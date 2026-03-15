namespace Egroo.Server.Test
{
    /// <summary>
    /// Tests the channel CRUD flow using the <c>jihadkhawaja.chat.server</c> hub
    /// backed by an in-memory EF Core database.
    /// These tests exercise <c>ChannelRepository</c> which implements
    /// <see cref="IChannel"/> defined in <c>jihadkhawaja.chat.shared</c>.
    /// </summary>
    [TestClass]
    public class ChannelTest
    {
        private const string DbName = "ChannelTestDb";

        // The signed-up user that acts as the authenticated caller throughout all tests.
        private Guid _userId;
        private IServiceProvider _authenticatedServices = null!;

        [TestInitialize]
        public async Task Initialize()
        {
            // 1. Sign up a user (no auth context needed for sign-up).
            var anonServices = TestServiceProvider.Build(dbName: DbName);
            using var signUpScope = anonServices.CreateScope();
            var auth = signUpScope.ServiceProvider.GetRequiredService<IAuth>();

            var signUp = await auth.SignUp("channeltester", "ValidP@ss1!");

            // Allow re-running the same in-memory DB (duplicate user is acceptable).
            if (!signUp.Success && !(signUp.Message?.Contains("exist", StringComparison.OrdinalIgnoreCase) ?? false))
                Assert.Fail($"Sign-up failed unexpectedly: {signUp.Message}");

            // 2. Sign in to get the real user ID.
            using var signInScope = anonServices.CreateScope();
            var signIn = await signInScope.ServiceProvider.GetRequiredService<IAuth>()
                                          .SignIn("channeltester", "ValidP@ss1!");
            Assert.IsTrue(signIn.Success, $"Sign-in failed: {signIn.Message}");
            _userId = signIn.UserId!.Value;

            // 3. Build a service provider that impersonates this user.
            _authenticatedServices = TestServiceProvider.Build(dbName: DbName, authenticatedUserId: _userId);
        }

        // ── Create ──────────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task CreateChannel_WithValidUsername_ReturnsChannel()
        {
            using var scope = _authenticatedServices.CreateScope();
            var channel = await scope.ServiceProvider.GetRequiredService<IChannel>()
                                     .CreateChannel("channeltester");

            Assert.IsNotNull(channel, "CreateChannel returned null.");
            Assert.AreNotEqual(Guid.Empty, channel.Id);
        }

        [TestMethod]
        public async Task CreateChannel_EmptyUsernames_ReturnsNull()
        {
            using var scope = _authenticatedServices.CreateScope();
            var channel = await scope.ServiceProvider.GetRequiredService<IChannel>()
                                     .CreateChannel();  // no usernames

            Assert.IsNull(channel, "Expected null when no usernames are supplied.");
        }

        // ── Read ────────────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task GetChannel_ExistingChannel_ReturnsChannel()
        {
            using var createScope = _authenticatedServices.CreateScope();
            var channelSvc = createScope.ServiceProvider.GetRequiredService<IChannel>();
            var created = await channelSvc.CreateChannel("channeltester");
            Assert.IsNotNull(created);

            using var readScope = _authenticatedServices.CreateScope();
            var fetched = await readScope.ServiceProvider.GetRequiredService<IChannel>()
                                         .GetChannel(created.Id);

            Assert.IsNotNull(fetched);
            Assert.AreEqual(created.Id, fetched.Id);
        }

        [TestMethod]
        public async Task GetChannel_NonExistentId_ReturnsNull()
        {
            using var scope = _authenticatedServices.CreateScope();
            var fetched = await scope.ServiceProvider.GetRequiredService<IChannel>()
                                     .GetChannel(Guid.NewGuid());

            Assert.IsNull(fetched);
        }

        [TestMethod]
        public async Task GetUserChannels_ReturnsAtLeastOneChannel()
        {
            // Ensure at least one channel exists for this user.
            using var createScope = _authenticatedServices.CreateScope();
            await createScope.ServiceProvider.GetRequiredService<IChannel>()
                             .CreateChannel("channeltester");

            using var readScope = _authenticatedServices.CreateScope();
            var channels = await readScope.ServiceProvider.GetRequiredService<IChannel>()
                                          .GetUserChannels();

            Assert.IsNotNull(channels);
            Assert.IsTrue(channels.Length > 0, "Expected at least one user channel.");
        }

        // ── Membership ──────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task ChannelContainUser_AfterCreate_ReturnsTrue()
        {
            using var createScope = _authenticatedServices.CreateScope();
            var channel = await createScope.ServiceProvider.GetRequiredService<IChannel>()
                                           .CreateChannel("channeltester");
            Assert.IsNotNull(channel);

            using var checkScope = _authenticatedServices.CreateScope();
            bool contains = await checkScope.ServiceProvider.GetRequiredService<IChannel>()
                                            .ChannelContainUser(channel.Id, _userId);

            Assert.IsTrue(contains, "User should be a member of the channel they created.");
        }

        [TestMethod]
        public async Task IsChannelAdmin_AfterCreate_ReturnsTrue()
        {
            using var createScope = _authenticatedServices.CreateScope();
            var channel = await createScope.ServiceProvider.GetRequiredService<IChannel>()
                                           .CreateChannel("channeltester");
            Assert.IsNotNull(channel);

            using var checkScope = _authenticatedServices.CreateScope();
            bool isAdmin = await checkScope.ServiceProvider.GetRequiredService<IChannel>()
                                           .IsChannelAdmin(channel.Id, _userId);

            Assert.IsTrue(isAdmin, "Channel creator should be the admin.");
        }

        // ── Delete / Leave ──────────────────────────────────────────────────────────

        [TestMethod]
        public async Task DeleteChannel_ExistingChannel_Succeeds()
        {
            using var createScope = _authenticatedServices.CreateScope();
            var channel = await createScope.ServiceProvider.GetRequiredService<IChannel>()
                                           .CreateChannel("channeltester");
            Assert.IsNotNull(channel);

            using var deleteScope = _authenticatedServices.CreateScope();
            bool deleted = await deleteScope.ServiceProvider.GetRequiredService<IChannel>()
                                            .DeleteChannel(channel.Id);

            Assert.IsTrue(deleted, "DeleteChannel should return true for an existing channel.");
        }

        [TestMethod]
        public async Task DeleteChannel_NonExistentId_ReturnsFalse()
        {
            using var scope = _authenticatedServices.CreateScope();
            bool deleted = await scope.ServiceProvider.GetRequiredService<IChannel>()
                                      .DeleteChannel(Guid.NewGuid());

            Assert.IsFalse(deleted, "DeleteChannel should return false for a non-existent channel.");
        }

        // ── AddChannelUsers / RemoveChannelUser ─────────────────────────────────────

        [TestMethod]
        public async Task AddChannelUsers_ValidUsername_ReturnsTrue()
        {
            // Sign up a second user
            var anonServices = TestServiceProvider.Build(dbName: DbName);
            using var signUpScope = anonServices.CreateScope();
            await signUpScope.ServiceProvider.GetRequiredService<IAuth>()
                             .SignUp("channeladd1", "ValidP@ss1!");

            // Create channel and add the second user
            using var createScope = _authenticatedServices.CreateScope();
            var channel = await createScope.ServiceProvider.GetRequiredService<IChannel>()
                                           .CreateChannel("channeltester");
            Assert.IsNotNull(channel);

            using var addScope = _authenticatedServices.CreateScope();
            bool added = await addScope.ServiceProvider.GetRequiredService<IChannel>()
                                       .AddChannelUsers(channel.Id, "channeladd1");

            Assert.IsTrue(added, "AddChannelUsers should succeed for a valid user.");
        }

        [TestMethod]
        public async Task GetChannelUsers_ReturnsMembers()
        {
            using var createScope = _authenticatedServices.CreateScope();
            var channel = await createScope.ServiceProvider.GetRequiredService<IChannel>()
                                           .CreateChannel("channeltester");
            Assert.IsNotNull(channel);

            using var getScope = _authenticatedServices.CreateScope();
            var users = await getScope.ServiceProvider.GetRequiredService<IChannel>()
                                      .GetChannelUsers(channel.Id);

            Assert.IsNotNull(users);
            Assert.IsTrue(users.Length > 0, "Should have at least the creator.");
        }

        [TestMethod]
        public async Task RemoveChannelUser_ValidUser_ReturnsTrue()
        {
            // Sign up a second user
            var anonServices = TestServiceProvider.Build(dbName: DbName);
            using var signUpScope = anonServices.CreateScope();
            var signUp = await signUpScope.ServiceProvider.GetRequiredService<IAuth>()
                                          .SignUp("channelrem1", "ValidP@ss1!");

            using var signInScope = anonServices.CreateScope();
            var signIn = await signInScope.ServiceProvider.GetRequiredService<IAuth>()
                                          .SignIn("channelrem1", "ValidP@ss1!");
            Assert.IsTrue(signIn.Success);

            // Create channel and add the second user
            using var createScope = _authenticatedServices.CreateScope();
            var channel = await createScope.ServiceProvider.GetRequiredService<IChannel>()
                                           .CreateChannel("channeltester", "channelrem1");
            Assert.IsNotNull(channel);

            using var removeScope = _authenticatedServices.CreateScope();
            bool removed = await removeScope.ServiceProvider.GetRequiredService<IChannel>()
                                            .RemoveChannelUser(channel.Id, signIn.UserId!.Value);

            Assert.IsTrue(removed, "RemoveChannelUser should succeed.");
        }

        [TestMethod]
        public async Task LeaveChannel_ReturnsTrue()
        {
            using var createScope = _authenticatedServices.CreateScope();
            var channel = await createScope.ServiceProvider.GetRequiredService<IChannel>()
                                           .CreateChannel("channeltester");
            Assert.IsNotNull(channel);

            using var leaveScope = _authenticatedServices.CreateScope();
            bool left = await leaveScope.ServiceProvider.GetRequiredService<IChannel>()
                                        .LeaveChannel(channel.Id);

            Assert.IsTrue(left, "LeaveChannel should succeed.");
        }

        [TestMethod]
        public async Task SearchPublicChannels_NoPublicChannels_ReturnsEmpty()
        {
            using var scope = _authenticatedServices.CreateScope();
            var results = await scope.ServiceProvider.GetRequiredService<IChannel>()
                                     .SearchPublicChannels("randomsearch");

            // No public channels exist so result should be empty or null
            Assert.IsTrue(results == null || results.Length == 0);
        }

        [TestMethod]
        public async Task ChannelContainUser_NonExistentChannel_ReturnsFalse()
        {
            using var scope = _authenticatedServices.CreateScope();
            bool contains = await scope.ServiceProvider.GetRequiredService<IChannel>()
                                       .ChannelContainUser(Guid.NewGuid(), _userId);

            Assert.IsFalse(contains);
        }

        [TestMethod]
        public async Task IsChannelAdmin_NonExistentChannel_ReturnsFalse()
        {
            using var scope = _authenticatedServices.CreateScope();
            bool isAdmin = await scope.ServiceProvider.GetRequiredService<IChannel>()
                                      .IsChannelAdmin(Guid.NewGuid(), _userId);

            Assert.IsFalse(isAdmin);
        }
    }
}
