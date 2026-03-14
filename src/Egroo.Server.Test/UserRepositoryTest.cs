using Egroo.Server.Database;
using Egroo.Server.Models;
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