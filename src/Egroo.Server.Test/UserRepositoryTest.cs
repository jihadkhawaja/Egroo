using Egroo.Server.Database;
using Microsoft.EntityFrameworkCore;

namespace Egroo.Server.Test
{
    [TestClass]
    public class UserRepositoryTest
    {
        private string _dbName = null!;
        private Guid _currentUserId;
        private Guid _friendUserId;
        private Guid _otherUserId;
        private IServiceProvider _authenticatedServices = null!;
        private IServiceProvider _friendServices = null!;

        [TestInitialize]
        public async Task Initialize()
        {
            _dbName = $"UserRepositoryTest_{Guid.NewGuid():N}";
            (_currentUserId, _) = await CreateUserAsync("repoowner");
            (_friendUserId, _) = await CreateUserAsync("repofriend");
            (_otherUserId, _) = await CreateUserAsync("repoother");

            _authenticatedServices = TestServiceProvider.Build(_dbName, _currentUserId);
            _friendServices = TestServiceProvider.Build(_dbName, _friendUserId);
        }

        [TestMethod]
        public async Task AddFriend_AcceptFriend_AndGetUserFriends_Succeeds()
        {
            using var ownerScope = _authenticatedServices.CreateScope();
            var ownerRepo = ownerScope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsTrue(await ownerRepo.AddFriend("repofriend"));

            using var friendScope = _friendServices.CreateScope();
            var friendRepo = friendScope.ServiceProvider.GetRequiredService<IUser>();
            var requests = await friendRepo.GetUserFriendRequests(_friendUserId);

            Assert.IsNotNull(requests);
            Assert.AreEqual(1, requests.Length);
            Assert.IsTrue(await friendRepo.AcceptFriend(_currentUserId));

            using var readScope = _authenticatedServices.CreateScope();
            var readRepo = readScope.ServiceProvider.GetRequiredService<IUser>();

            var friends = await readRepo.GetUserFriends(_currentUserId);
            var isFriend = await readRepo.GetUserIsFriend(_currentUserId, _friendUserId);

            Assert.IsNotNull(friends);
            Assert.AreEqual(1, friends.Length);
            Assert.IsTrue(isFriend);
        }

        [TestMethod]
        public async Task DenyFriend_RemovesPendingRequest()
        {
            using var ownerScope = _authenticatedServices.CreateScope();
            var ownerRepo = ownerScope.ServiceProvider.GetRequiredService<IUser>();
            Assert.IsTrue(await ownerRepo.AddFriend("repofriend"));

            using var friendScope = _friendServices.CreateScope();
            var friendRepo = friendScope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsTrue(await friendRepo.DenyFriend(_currentUserId));

            var requests = await friendRepo.GetUserFriendRequests(_friendUserId);
            Assert.IsNotNull(requests);
            Assert.AreEqual(0, requests.Length);
        }

        [TestMethod]
        public async Task PublicDetails_AndEncryptionKey_AreReturnedAndPersisted()
        {
            using var ownerScope = _authenticatedServices.CreateScope();
            var ownerRepo = ownerScope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsTrue(await ownerRepo.UpdateDetails("Repo Owner", "repo@example.com", "Repo", "Owner"));
            Assert.IsTrue(await ownerRepo.UpdateEncryptionKey("public-key", "key-1"));

            var publicDetails = await ownerRepo.GetUserPublicDetails(_currentUserId);
            var privateDetails = await ownerRepo.GetUserPrivateDetails();

            Assert.IsNotNull(publicDetails);
            Assert.AreEqual("repoowner", publicDetails.Username);
            Assert.AreEqual("Repo Owner", publicDetails.UserDetail?.DisplayName);
            Assert.AreEqual("public-key", publicDetails.EncryptionPublicKey);
            Assert.AreEqual("key-1", publicDetails.EncryptionKeyId);

            Assert.IsNotNull(privateDetails);
            Assert.AreEqual("repo@example.com", privateDetails.UserDetail?.Email);
        }

        [TestMethod]
        public async Task SearchUser_ExcludesCurrentUser_AndFindsMatches()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            var users = await repo.SearchUser("repo");

            Assert.IsNotNull(users);
            Assert.AreEqual(2, users.Count());
            CollectionAssert.DoesNotContain(users.Select(x => x.Username).ToList(), "repoowner");
        }

        [TestMethod]
        public async Task UsernameAndProfileHelpers_ReturnExpectedValues()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.AreEqual("repoowner", await repo.GetCurrentUserUsername());
            Assert.IsFalse(await repo.IsUsernameAvailable("repoowner"));
            Assert.IsTrue(await repo.IsUsernameAvailable("brandnewuser"));
            Assert.IsFalse(await repo.IsUsernameAvailable(" "));
        }

        [TestMethod]
        public async Task UpdateDetails_PersistsPrivateProfileFields()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsTrue(await repo.UpdateDetails("Display Name", "mail@example.com", "Repo", "Owner"));

            var profile = await repo.GetUserPrivateDetails();
            Assert.IsNotNull(profile);
            Assert.AreEqual("Display Name", profile.UserDetail?.DisplayName);
            Assert.AreEqual("mail@example.com", profile.UserDetail?.Email);
            Assert.AreEqual("Repo", profile.UserDetail?.FirstName);
            Assert.AreEqual("Owner", profile.UserDetail?.LastName);
        }

        [TestMethod]
        public async Task AvatarAndCover_ReturnStoredMedia()
        {
            using (var scope = _authenticatedServices.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DataContext>();
                var user = await db.Users.Include(x => x.UserStorage).FirstAsync(x => x.Id == _currentUserId);
                user.UserStorage = new UserStorage
                {
                    UserId = _currentUserId,
                    AvatarContentType = "png",
                    AvatarImageBase64 = "avatar-base64",
                    CoverContentType = "jpeg",
                    CoverImageBase64 = "cover-base64"
                };
                db.Users.Update(user);
                await db.SaveChangesAsync();
            }

            using var readScope = _authenticatedServices.CreateScope();
            var repo = readScope.ServiceProvider.GetRequiredService<IUser>();

            var avatar = await repo.GetAvatar(_currentUserId);
            var cover = await repo.GetCover(_currentUserId);

            Assert.IsNotNull(avatar);
            Assert.AreEqual("png", avatar.ContentType);
            Assert.AreEqual("avatar-base64", avatar.ImageBase64);
            Assert.IsNotNull(cover);
            Assert.AreEqual("jpeg", cover.ContentType);
            Assert.AreEqual("cover-base64", cover.ImageBase64);
        }

        [TestMethod]
        public async Task SendFeedback_PersistsEntry()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();

            Assert.IsTrue(await repo.SendFeedback("Useful feedback"));

            var user = await db.Users.Include(x => x.UserFeedbacks).FirstAsync(x => x.Id == _currentUserId);
            Assert.AreEqual(1, user.UserFeedbacks.Count);
            Assert.AreEqual("Useful feedback", user.UserFeedbacks.Single().Text);
        }

        [TestMethod]
        public async Task CloseUserSession_MarksUserOffline()
        {
            using (var seedScope = _authenticatedServices.CreateScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<DataContext>();
                var user = await db.Users.FirstAsync(x => x.Id == _currentUserId);
                user.IsOnline = true;
                user.ConnectionId = "connection-1";
                db.Users.Update(user);
                await db.SaveChangesAsync();
            }

            using (var scope = _authenticatedServices.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IUser>();
                await repo.CloseUserSession();
            }

            using (var verifyScope = TestServiceProvider.Build(_dbName).CreateScope())
            {
                var db = verifyScope.ServiceProvider.GetRequiredService<DataContext>();
                var user = await db.Users.FirstAsync(x => x.Id == _currentUserId);
                Assert.IsFalse(user.IsOnline);
                Assert.IsNull(user.ConnectionId);
            }
        }

        // ── RemoveFriend ────────────────────────────────────────────────────────

        [TestMethod]
        public async Task RemoveFriend_ExistingFriendship_Succeeds()
        {
            // Insert accepted friendship directly via DB to avoid cross-scope tracking issues
            using (var dbScope = _authenticatedServices.CreateScope())
            {
                var db = dbScope.ServiceProvider.GetRequiredService<DataContext>();
                db.UsersFriends.Add(new UserFriend
                {
                    Id = Guid.NewGuid(),
                    UserId = _currentUserId,
                    FriendUserId = _friendUserId,
                    DateCreated = DateTime.UtcNow,
                    DateAcceptedOn = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
            }

            using (var scope = _authenticatedServices.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IUser>();
                Assert.IsTrue(await repo.RemoveFriend("repofriend"), "RemoveFriend should succeed");

                var friends = await repo.GetUserFriends(_currentUserId);
                Assert.IsNotNull(friends);
                Assert.AreEqual(0, friends.Length);
            }
        }

        [TestMethod]
        public async Task RemoveFriend_EmptyUsername_ReturnsFalse()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.RemoveFriend(""));
        }

        [TestMethod]
        public async Task RemoveFriend_NonexistentUser_ReturnsFalse()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.RemoveFriend("ghostuser"));
        }

        [TestMethod]
        public async Task RemoveFriend_NoFriendship_ReturnsFalse()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.RemoveFriend("repoother"));
        }

        // ── AddFriend edge cases ─────────────────────────────────────────────────

        [TestMethod]
        public async Task AddFriend_EmptyUsername_ReturnsFalse()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.AddFriend(""));
        }

        [TestMethod]
        public async Task AddFriend_Self_ReturnsFalse()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.AddFriend("repoowner"));
        }

        [TestMethod]
        public async Task AddFriend_NonExistentUser_ReturnsFalse()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.AddFriend("nonexistent_user"));
        }

        [TestMethod]
        public async Task AddFriend_DuplicateRequest_ReturnsFalse()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsTrue(await repo.AddFriend("repoother"));
            Assert.IsFalse(await repo.AddFriend("repoother"), "Duplicate friend request should fail.");
        }

        // ── SearchUserFriends ────────────────────────────────────────────────────

        [TestMethod]
        public async Task SearchUserFriends_WithAcceptedFriend_ReturnsFriend()
        {
            // Create friendship inline with explicit scope disposal between steps
            using (var addScope = _authenticatedServices.CreateScope())
            {
                var addRepo = addScope.ServiceProvider.GetRequiredService<IUser>();
                Assert.IsTrue(await addRepo.AddFriend("repofriend"), "AddFriend should succeed");
            }

            using (var acceptScope = _friendServices.CreateScope())
            {
                var acceptRepo = acceptScope.ServiceProvider.GetRequiredService<IUser>();
                Assert.IsTrue(await acceptRepo.AcceptFriend(_currentUserId), "AcceptFriend should succeed");
            }

            using (var searchScope = _authenticatedServices.CreateScope())
            {
                var searchRepo = searchScope.ServiceProvider.GetRequiredService<IUser>();
                var results = await searchRepo.SearchUserFriends("repo");
                Assert.IsNotNull(results);
                Assert.IsTrue(results.Any(), "Expected at least one friend matching 'repo'.");
            }
        }

        [TestMethod]
        public async Task SearchUserFriends_NoFriends_ReturnsEmptyList()
        {
            // Other user has no accepted friends
            var otherServices = TestServiceProvider.Build(_dbName, _otherUserId);
            using var scope = otherServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            var results = await repo.SearchUserFriends("repo");
            Assert.IsNotNull(results);
            Assert.AreEqual(0, results.Count());
        }

        // ── DeleteUser ──────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task DeleteUser_ExistingUser_RemovesUserAndRelatedData()
        {
            // Create a fresh isolated user for this test
            var name = $"deletetest_{Guid.NewGuid():N}"[..16];
            var (deleteUserId, _) = await CreateUserAsync(name);
            var deleteServices = TestServiceProvider.Build(_dbName, deleteUserId);

            // Create a channel
            using (var chanScope = deleteServices.CreateScope())
            {
                var chanRepo = chanScope.ServiceProvider.GetRequiredService<IChannel>();
                await chanRepo.CreateChannel(name);
            }

            // Delete the user
            using (var scope = deleteServices.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IUser>();
                Assert.IsTrue(await repo.DeleteUser());
            }

            // Verify user is gone
            using (var verifyScope = TestServiceProvider.Build(_dbName).CreateScope())
            {
                var db = verifyScope.ServiceProvider.GetRequiredService<DataContext>();
                var user = await db.Users.FirstOrDefaultAsync(x => x.Id == deleteUserId);
                Assert.IsNull(user, "User should be deleted.");
            }
        }

        [TestMethod]
        public async Task DeleteUser_Unauthenticated_ReturnsFalse()
        {
            var anonServices = TestServiceProvider.Build(_dbName);
            using var scope = anonServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.DeleteUser());
        }

        // ── AcceptFriend / DenyFriend edge cases ────────────────────────────────────

        [TestMethod]
        public async Task AcceptFriend_NoRequest_ReturnsFalse()
        {
            using var scope = _friendServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.AcceptFriend(Guid.NewGuid()));
        }

        [TestMethod]
        public async Task DenyFriend_NoRequest_ReturnsFalse()
        {
            using var scope = _friendServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.DenyFriend(Guid.NewGuid()));
        }

        // ── GetUserIsFriend edge case ───────────────────────────────────────────────

        [TestMethod]
        public async Task GetUserIsFriend_NotFriends_ReturnsFalse()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.GetUserIsFriend(_currentUserId, Guid.NewGuid()));
        }

        // ── GetUserPublicDetails edge cases ─────────────────────────────────────────

        [TestMethod]
        public async Task GetUserPublicDetails_NonexistentUser_ReturnsNull()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsNull(await repo.GetUserPublicDetails(Guid.NewGuid()));
        }

        // ── GetUserPrivateDetails edge case ─────────────────────────────────────────

        [TestMethod]
        public async Task GetUserPrivateDetails_Unauthenticated_ReturnsNull()
        {
            var anonServices = TestServiceProvider.Build(_dbName);
            using var scope = anonServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsNull(await repo.GetUserPrivateDetails());
        }

        // ── GetCurrentUserUsername edge case ─────────────────────────────────────────

        [TestMethod]
        public async Task GetCurrentUserUsername_Unauthenticated_ReturnsNull()
        {
            var anonServices = TestServiceProvider.Build(_dbName);
            using var scope = anonServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsNull(await repo.GetCurrentUserUsername());
        }

        // ── UpdateEncryptionKey edge cases ──────────────────────────────────────────

        [TestMethod]
        public async Task UpdateEncryptionKey_Unauthenticated_ReturnsFalse()
        {
            var anonServices = TestServiceProvider.Build(_dbName);
            using var scope = anonServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.UpdateEncryptionKey("key", "id"));
        }

        [TestMethod]
        public async Task UpdateEncryptionKey_ClearKey_SetsNull()
        {
            using (var scope = _authenticatedServices.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IUser>();
                Assert.IsTrue(await repo.UpdateEncryptionKey("public-key", "key-id"));
            }

            using (var clearScope = _authenticatedServices.CreateScope())
            {
                var repo = clearScope.ServiceProvider.GetRequiredService<IUser>();
                Assert.IsTrue(await repo.UpdateEncryptionKey("", ""));
            }

            using (var verifyScope = _authenticatedServices.CreateScope())
            {
                var repo = verifyScope.ServiceProvider.GetRequiredService<IUser>();
                var profile = await repo.GetUserPrivateDetails();
                Assert.IsNotNull(profile);
                Assert.IsNull(profile.EncryptionPublicKey);
                Assert.IsNull(profile.EncryptionKeyId);
                Assert.IsNull(profile.EncryptionKeyUpdatedOn);
            }
        }

        // ── UpdateDetails edge case ─────────────────────────────────────────────────

        [TestMethod]
        public async Task UpdateDetails_Unauthenticated_ReturnsFalse()
        {
            var anonServices = TestServiceProvider.Build(_dbName);
            using var scope = anonServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.UpdateDetails("name", "email", "first", "last"));
        }

        // ── SendFeedback edge cases ─────────────────────────────────────────────────

        [TestMethod]
        public async Task SendFeedback_EmptyText_ReturnsFalse()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.SendFeedback(""));
        }

        [TestMethod]
        public async Task SendFeedback_Unauthenticated_ReturnsFalse()
        {
            var anonServices = TestServiceProvider.Build(_dbName);
            using var scope = anonServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.SendFeedback("feedback"));
        }

        // ── GetAvatar / GetCover edge cases ─────────────────────────────────────────

        [TestMethod]
        public async Task GetAvatar_NoAvatar_ReturnsNull()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsNull(await repo.GetAvatar(Guid.NewGuid()));
        }

        [TestMethod]
        public async Task GetCover_NoCover_ReturnsNull()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsNull(await repo.GetCover(Guid.NewGuid()));
        }

        // ── UpdateAvatar / UpdateCover edge cases ───────────────────────────────────

        [TestMethod]
        public async Task UpdateAvatar_EmptyBase64_ReturnsFalse()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.UpdateAvatar(""));
        }

        [TestMethod]
        public async Task UpdateAvatar_Unauthenticated_ReturnsFalse()
        {
            var anonServices = TestServiceProvider.Build(_dbName);
            using var scope = anonServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.UpdateAvatar("dGVzdA=="));
        }

        [TestMethod]
        public async Task UpdateCover_EmptyBase64_ReturnsFalse()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.UpdateCover(""));
        }

        [TestMethod]
        public async Task UpdateCover_Unauthenticated_ReturnsFalse()
        {
            var anonServices = TestServiceProvider.Build(_dbName);
            using var scope = anonServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.UpdateCover("dGVzdA=="));
        }

        // ── CloseUserSession edge case ──────────────────────────────────────────────

        [TestMethod]
        public async Task CloseUserSession_Unauthenticated_NoOp()
        {
            var anonServices = TestServiceProvider.Build(_dbName);
            using var scope = anonServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            // Should silently return without error
            await repo.CloseUserSession();
        }

        // ── GetUserFriendRequests edge case ─────────────────────────────────────────

        [TestMethod]
        public async Task GetUserFriendRequests_NoRequests_ReturnsEmpty()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            var requests = await repo.GetUserFriendRequests(_currentUserId);
            Assert.IsNotNull(requests);
            Assert.AreEqual(0, requests.Length);
        }

        // ── Multi-device encryption key management ────────────────────────────────

        [TestMethod]
        public async Task AddEncryptionKey_AddsMultipleDeviceKeys()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsTrue(await repo.AddEncryptionKey("pk-device-1", "kid-1", "Chrome on Windows"));
            Assert.IsTrue(await repo.AddEncryptionKey("pk-device-2", "kid-2", "Safari on iPhone"));

            var keys = await repo.GetEncryptionKeys();
            Assert.IsNotNull(keys);
            Assert.AreEqual(2, keys.Length);
            Assert.IsTrue(keys.Any(k => k.KeyId == "kid-1" && k.DeviceLabel == "Chrome on Windows"));
            Assert.IsTrue(keys.Any(k => k.KeyId == "kid-2" && k.DeviceLabel == "Safari on iPhone"));
        }

        [TestMethod]
        public async Task AddEncryptionKey_SameKeyId_UpdatesExisting()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsTrue(await repo.AddEncryptionKey("pk-original", "kid-1", "Device A"));
            Assert.IsTrue(await repo.AddEncryptionKey("pk-updated", "kid-1", "Device A Updated"));

            var keys = await repo.GetEncryptionKeys();
            Assert.IsNotNull(keys);
            Assert.AreEqual(1, keys.Length);
            Assert.AreEqual("pk-updated", keys[0].PublicKey);
            Assert.AreEqual("Device A Updated", keys[0].DeviceLabel);
        }

        [TestMethod]
        public async Task AddEncryptionKey_UpdatesLegacyFieldsOnUser()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsTrue(await repo.AddEncryptionKey("pk-device-1", "kid-1", null));

            var profile = await repo.GetUserPrivateDetails();
            Assert.IsNotNull(profile);
            Assert.AreEqual("pk-device-1", profile.EncryptionPublicKey);
            Assert.AreEqual("kid-1", profile.EncryptionKeyId);
            Assert.IsNotNull(profile.EncryptionKeyUpdatedOn);
        }

        [TestMethod]
        public async Task AddEncryptionKey_MaxTenKeys_RejectsFurther()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(await repo.AddEncryptionKey($"pk-{i}", $"kid-{i}", $"Device {i}"));
            }

            Assert.IsFalse(await repo.AddEncryptionKey("pk-11", "kid-11", "Device 11"));

            var keys = await repo.GetEncryptionKeys();
            Assert.IsNotNull(keys);
            Assert.AreEqual(10, keys.Length);
        }

        [TestMethod]
        public async Task RemoveEncryptionKey_SoftDeletesKey()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsTrue(await repo.AddEncryptionKey("pk-1", "kid-1", "Device 1"));
            Assert.IsTrue(await repo.AddEncryptionKey("pk-2", "kid-2", "Device 2"));

            Assert.IsTrue(await repo.RemoveEncryptionKey("kid-1"));

            var keys = await repo.GetEncryptionKeys();
            Assert.IsNotNull(keys);
            Assert.AreEqual(1, keys.Length);
            Assert.AreEqual("kid-2", keys[0].KeyId);
        }

        [TestMethod]
        public async Task RemoveEncryptionKey_UpdatesLegacyFieldsToRemainingKey()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsTrue(await repo.AddEncryptionKey("pk-1", "kid-1", null));
            Assert.IsTrue(await repo.AddEncryptionKey("pk-2", "kid-2", null));

            Assert.IsTrue(await repo.RemoveEncryptionKey("kid-2"));

            var profile = await repo.GetUserPrivateDetails();
            Assert.IsNotNull(profile);
            Assert.AreEqual("pk-1", profile.EncryptionPublicKey);
            Assert.AreEqual("kid-1", profile.EncryptionKeyId);
        }

        [TestMethod]
        public async Task RemoveEncryptionKey_LastKey_ClearsLegacyFields()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsTrue(await repo.AddEncryptionKey("pk-1", "kid-1", null));
            Assert.IsTrue(await repo.RemoveEncryptionKey("kid-1"));

            var profile = await repo.GetUserPrivateDetails();
            Assert.IsNotNull(profile);
            Assert.IsNull(profile.EncryptionPublicKey);
            Assert.IsNull(profile.EncryptionKeyId);
        }

        [TestMethod]
        public async Task RemoveEncryptionKey_NonExistentKey_ReturnsFalse()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.RemoveEncryptionKey("doesnt-exist"));
        }

        [TestMethod]
        public async Task AddEncryptionKey_Unauthenticated_ReturnsFalse()
        {
            var anonServices = TestServiceProvider.Build(_dbName);
            using var scope = anonServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.AddEncryptionKey("pk", "kid", null));
        }

        [TestMethod]
        public async Task GetEncryptionKeys_Unauthenticated_ReturnsNull()
        {
            var anonServices = TestServiceProvider.Build(_dbName);
            using var scope = anonServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsNull(await repo.GetEncryptionKeys());
        }

        [TestMethod]
        public async Task RemoveEncryptionKey_Unauthenticated_ReturnsFalse()
        {
            var anonServices = TestServiceProvider.Build(_dbName);
            using var scope = anonServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsFalse(await repo.RemoveEncryptionKey("kid"));
        }

        [TestMethod]
        public async Task UpdateEncryptionKey_LegacyAddsKeyToCollection()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            Assert.IsTrue(await repo.UpdateEncryptionKey("pk-legacy", "kid-legacy"));

            var keys = await repo.GetEncryptionKeys();
            Assert.IsNotNull(keys);
            Assert.AreEqual(1, keys.Length);
            Assert.AreEqual("pk-legacy", keys[0].PublicKey);
            Assert.AreEqual("kid-legacy", keys[0].KeyId);
        }

        [TestMethod]
        public async Task RemoveEncryptionKey_RemovedKeyAllowsSlotForNewKey()
        {
            using var scope = _authenticatedServices.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUser>();

            // Fill up 10 keys
            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(await repo.AddEncryptionKey($"pk-{i}", $"kid-{i}", null));
            }

            // Remove one
            Assert.IsTrue(await repo.RemoveEncryptionKey("kid-5"));

            // Now can add a new one
            Assert.IsTrue(await repo.AddEncryptionKey("pk-new", "kid-new", "New Device"));

            var keys = await repo.GetEncryptionKeys();
            Assert.IsNotNull(keys);
            Assert.AreEqual(10, keys.Length);
        }

        private async Task CreateAcceptedFriendshipAsync()
        {
            using var ownerScope = _authenticatedServices.CreateScope();
            var ownerRepo = ownerScope.ServiceProvider.GetRequiredService<IUser>();
            await ownerRepo.AddFriend("repofriend");

            using var friendScope = _friendServices.CreateScope();
            var friendRepo = friendScope.ServiceProvider.GetRequiredService<IUser>();
            await friendRepo.AcceptFriend(_currentUserId);
        }

        private async Task<(Guid UserId, string Username)> CreateUserAsync(string username)
        {
            var services = TestServiceProvider.Build(_dbName);
            using var scope = services.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<IAuth>();
            var result = await auth.SignUp(username, "ValidP@ss1!");
            Assert.IsTrue(result.Success, $"Failed to create user {username}: {result.Message}");
            return (result.UserId!.Value, username);
        }
    }
}