using jihadkhawaja.chat.server.Hubs;
using jihadkhawaja.chat.server.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Egroo.Server.Test;

[TestClass]
public class ChatHubTest
{
    // ── Mock infrastructure ─────────────────────────────────────────────────

    /// <summary>Records all SendAsync calls for assertion.</summary>
    private sealed class FakeClientProxy : IClientProxy
    {
        public List<(string Method, object?[] Args)> Calls { get; } = new();

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Calls.Add((method, args));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHubClients : IHubCallerClients
    {
        public FakeClientProxy AllProxy { get; } = new();
        public FakeClientProxy CallerProxy { get; } = new();
        private readonly FakeClientProxy _defaultProxy = new();

        public List<(IReadOnlyList<string> ConnectionIds, FakeClientProxy Proxy)> ClientCalls { get; } = new();

        public IClientProxy All => AllProxy;
        public IClientProxy Caller => CallerProxy;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _defaultProxy;
        public IClientProxy Client(string connectionId)
        {
            var proxy = new FakeClientProxy();
            ClientCalls.Add((new[] { connectionId }, proxy));
            return proxy;
        }

        public IClientProxy Clients(IReadOnlyList<string> connectionIds)
        {
            var proxy = new FakeClientProxy();
            ClientCalls.Add((connectionIds, proxy));
            return proxy;
        }

        public IClientProxy Group(string groupName) => _defaultProxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _defaultProxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => _defaultProxy;
        public IClientProxy OthersInGroup(string groupName) => _defaultProxy;
        public IClientProxy Others => _defaultProxy;
        public IClientProxy User(string userId) => _defaultProxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => _defaultProxy;
    }

    private sealed class FakeHubContext : HubCallerContext
    {
        private readonly Guid? _userId;
        private readonly string _connectionId;

        public FakeHubContext(Guid? userId, string connectionId = "test-conn-1")
        {
            _userId = userId;
            _connectionId = connectionId;
        }

        public override string ConnectionId => _connectionId;
        public override string? UserIdentifier => _userId?.ToString();

        public override ClaimsPrincipal? User
        {
            get
            {
                if (!_userId.HasValue) return null;
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, _userId.Value.ToString()),
                    new(ClaimTypes.Name, "testuser"),
                };
                return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
            }
        }

        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }

        public override IFeatureCollection Features =>
            throw new NotImplementedException();
    }

    /// <summary>
    /// Minimal IUser implementation that returns canned results for ChatHub delegation tests.
    /// </summary>
    private sealed class StubUserRepository : IUser
    {
        public UserDto?[] UserPublicDetailsResults { get; set; } = Array.Empty<UserDto?>();
        private int _publicDetailsCallIndex;

        public Task<UserDto?> GetUserPublicDetails(Guid userId)
        {
            if (_publicDetailsCallIndex < UserPublicDetailsResults.Length)
                return Task.FromResult(UserPublicDetailsResults[_publicDetailsCallIndex++]);
            return Task.FromResult<UserDto?>(null);
        }

        public Task<UserFriend[]?> Friends { get; set; } = Task.FromResult<UserFriend[]?>(null);
        public Task<UserFriend[]?> GetUserFriends(Guid userId) => Friends;

        // Stubs for interface compliance
        public Task CloseUserSession() => Task.CompletedTask;
        public Task<UserDto?> GetUserPrivateDetails() => Task.FromResult<UserDto?>(null);
        public Task<string?> GetCurrentUserUsername() => Task.FromResult<string?>(null);
        public Task<bool> AddFriend(string friendusername) => Task.FromResult(false);
        public Task<bool> RemoveFriend(string friendusername) => Task.FromResult(false);
        public Task<UserFriend[]?> GetUserFriendRequests(Guid userId) => Task.FromResult<UserFriend[]?>(null);
        public Task<bool> GetUserIsFriend(Guid userId, Guid friendId) => Task.FromResult(false);
        public Task<bool> AcceptFriend(Guid friendId) => Task.FromResult(false);
        public Task<bool> DenyFriend(Guid friendId) => Task.FromResult(false);
        public Task<IEnumerable<UserDto>?> SearchUser(string query, int maxResult = 20) => Task.FromResult<IEnumerable<UserDto>?>(null);
        public Task<IEnumerable<UserDto>?> SearchUserFriends(string query, int maxResult = 20) => Task.FromResult<IEnumerable<UserDto>?>(null);
        public Task<bool> IsUsernameAvailable(string username) => Task.FromResult(true);
        public Task<bool> DeleteUser() => Task.FromResult(false);
        public Task<bool> UpdateDetails(string? d, string? e, string? f, string? l) => Task.FromResult(false);
        public Task<bool> UpdateEncryptionKey(string? pk, string? ki) => Task.FromResult(false);
        public Task<bool> AddEncryptionKey(string pk, string ki, string? dl) => Task.FromResult(false);
        public Task<bool> RemoveEncryptionKey(string ki) => Task.FromResult(false);
        public Task<UserEncryptionKeyInfo[]?> GetEncryptionKeys() => Task.FromResult<UserEncryptionKeyInfo[]?>(null);
        public Task<bool> UpdateAvatar(string? a) => Task.FromResult(false);
        public Task<bool> UpdateCover(string? c) => Task.FromResult(false);
        public Task<bool> SendFeedback(string text) => Task.FromResult(false);
        public Task<MediaResult?> GetAvatar(Guid userId) => Task.FromResult<MediaResult?>(null);
        public Task<MediaResult?> GetCover(Guid userId) => Task.FromResult<MediaResult?>(null);
    }

    private sealed class StubChannelRepository : IChannel
    {
        public Dictionary<Guid, UserDto[]> ChannelUsers { get; } = new();
        public Dictionary<Guid, bool> MembershipResults { get; } = new();

        public Task<bool> ChannelContainUser(Guid channelId, Guid userId)
        {
            var key = channelId;
            return Task.FromResult(MembershipResults.ContainsKey(key) && MembershipResults[key]);
        }

        public Task<UserDto[]?> GetChannelUsers(Guid channelId)
        {
            ChannelUsers.TryGetValue(channelId, out var users);
            return Task.FromResult<UserDto[]?>(users);
        }

        // Stubs for interface compliance
        public Task<Channel?> CreateChannel(params string[] usernames) => Task.FromResult<Channel?>(null);
        public Task<bool> AddChannelUsers(Guid channelId, params string[] usernames) => Task.FromResult(false);
        public Task<bool> RemoveChannelUser(Guid channelId, Guid userId) => Task.FromResult(false);
        public Task<Channel[]?> GetUserChannels() => Task.FromResult<Channel[]?>(null);
        public Task<Channel?> GetChannel(Guid channelId) => Task.FromResult<Channel?>(null);
        public Task<bool> IsChannelAdmin(Guid channelId, Guid userId) => Task.FromResult(false);
        public Task<bool> DeleteChannel(Guid channelId) => Task.FromResult(false);
        public Task<bool> LeaveChannel(Guid channelId) => Task.FromResult(false);
        public Task<Channel[]?> SearchPublicChannels(string searchTerm) => Task.FromResult<Channel[]?>(null);
    }

    private sealed class StubMessageRepository : IMessageRepository
    {
        public bool SendMessageResult { get; set; } = true;
        public bool AddPendingResult { get; set; } = true;

        public Task<bool> SendMessage(Message message) => Task.FromResult(SendMessageResult);
        public Task<bool> AddPendingMessage(UserPendingMessage pm) => Task.FromResult(AddPendingResult);
        public Task<bool> UpdateMessage(Message message) => Task.FromResult(true);
        public Task<Message?> GetMessageById(Guid messageId) => Task.FromResult<Message?>(null);
        public Task<Message?> GetMessageByReferenceId(Guid referenceId) => Task.FromResult<Message?>(null);
        public Task<IEnumerable<UserPendingMessage>> GetPendingMessagesForUser(Guid userId) => Task.FromResult<IEnumerable<UserPendingMessage>>(Array.Empty<UserPendingMessage>());
        public Task UpdatePendingMessage(Guid messageId) => Task.CompletedTask;
        public string DecryptContent(string content) => content;
    }

    // ── Helper to create ChatHub with mocked internals ──────────────────────

    private static ChatHub CreateHub(
        Guid? userId,
        IConnectionTracker? tracker = null,
        StubUserRepository? userRepo = null,
        StubChannelRepository? channelRepo = null,
        StubMessageRepository? messageRepo = null,
        IAgentChannelResponder? agentResponder = null,
        string connectionId = "test-conn-1")
    {
        tracker ??= new InMemoryConnectionTracker();
        userRepo ??= new StubUserRepository();
        channelRepo ??= new StubChannelRepository();
        messageRepo ??= new StubMessageRepository();

        var hub = new ChatHub(tracker, userRepo, channelRepo, messageRepo, agentResponder);

        // Inject mock context and clients
        var context = new FakeHubContext(userId, connectionId);
        var clients = new FakeHubClients();

        // Use reflection to set the protected Hub.Context and Hub.Clients properties
        typeof(Hub).GetProperty("Context")!.SetValue(hub, context);
        typeof(Hub).GetProperty("Clients")!.SetValue(hub, clients);

        return hub;
    }

    private static FakeHubClients GetClients(ChatHub hub)
    {
        return (FakeHubClients)typeof(Hub).GetProperty("Clients")!.GetValue(hub)!;
    }

    // ── OnConnectedAsync / OnDisconnectedAsync ──────────────────────────────

    [TestMethod]
    public async Task OnConnectedAsync_AuthenticatedUser_TracksConnection()
    {
        var userId = Guid.NewGuid();
        var tracker = new InMemoryConnectionTracker();
        var hub = CreateHub(userId, tracker: tracker, connectionId: "conn-1");

        await hub.OnConnectedAsync();

        Assert.IsTrue(tracker.IsUserOnline(userId));
        Assert.AreEqual(1, tracker.GetConnectionCount(userId));
    }

    [TestMethod]
    public async Task OnConnectedAsync_UnauthenticatedUser_DoesNotTrack()
    {
        var tracker = new InMemoryConnectionTracker();
        var hub = CreateHub(null, tracker: tracker);

        await hub.OnConnectedAsync();

        // No user tracked
        Assert.AreEqual(0, tracker.GetConnectionCount(Guid.Empty));
    }

    [TestMethod]
    public async Task OnDisconnectedAsync_LastConnection_UntracksUser()
    {
        var userId = Guid.NewGuid();
        var tracker = new InMemoryConnectionTracker();
        tracker.TrackConnection(userId, "conn-1");

        var hub = CreateHub(userId, tracker: tracker, connectionId: "conn-1");

        await hub.OnDisconnectedAsync(null);

        Assert.IsFalse(tracker.IsUserOnline(userId));
    }

    [TestMethod]
    public async Task OnDisconnectedAsync_UnauthenticatedUser_NoException()
    {
        var hub = CreateHub(null);
        // Should not throw
        await hub.OnDisconnectedAsync(null);
    }

    // ── ChatHubChannel delegation ───────────────────────────────────────────

    [TestMethod]
    public async Task ChannelContainUser_DelegatesToRepository()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var channelRepo = new StubChannelRepository();
        channelRepo.MembershipResults[channelId] = true;

        var hub = CreateHub(userId, channelRepo: channelRepo);

        var result = await hub.ChannelContainUser(channelId, userId);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task GetChannelUsers_DelegatesToRepository()
    {
        var channelId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var user = new UserDto { Id = userId, Username = "test" };
        var channelRepo = new StubChannelRepository();
        channelRepo.ChannelUsers[channelId] = new[] { user };

        var hub = CreateHub(userId, channelRepo: channelRepo);

        var result = await hub.GetChannelUsers(channelId);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Length);
    }

    [TestMethod]
    public async Task GetUserChannels_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.GetUserChannels();
        Assert.IsNull(result); // stub returns null
    }

    [TestMethod]
    public async Task GetChannel_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.GetChannel(Guid.NewGuid());
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task IsChannelAdmin_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.IsChannelAdmin(Guid.NewGuid(), Guid.NewGuid());
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SearchPublicChannels_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.SearchPublicChannels("test");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CreateChannel_NullResult_ReturnsNull()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.CreateChannel("user1");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteChannel_StubReturnsFalse()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.DeleteChannel(Guid.NewGuid());
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task LeaveChannel_StubReturnsFalse()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.LeaveChannel(Guid.NewGuid());
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task AddChannelUsers_StubReturnsFalse()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.AddChannelUsers(Guid.NewGuid(), "user1");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task RemoveChannelUser_StubReturnsFalse()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.RemoveChannelUser(Guid.NewGuid(), Guid.NewGuid());
        Assert.IsFalse(result);
    }

    // ── ChatHubUser delegation ──────────────────────────────────────────────

    [TestMethod]
    public async Task GetUserPublicDetails_DelegatesToRepository()
    {
        var targetId = Guid.NewGuid();
        var userRepo = new StubUserRepository
        {
            UserPublicDetailsResults = new[] { new UserDto { Id = targetId, Username = "alice" } }
        };
        var hub = CreateHub(Guid.NewGuid(), userRepo: userRepo);

        var result = await hub.GetUserPublicDetails(targetId);

        Assert.IsNotNull(result);
        Assert.AreEqual("alice", result!.Username);
    }

    [TestMethod]
    public async Task GetUserPrivateDetails_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.GetUserPrivateDetails();
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetCurrentUserUsername_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.GetCurrentUserUsername();
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task AddFriend_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.AddFriend("someuser");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task RemoveFriend_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.RemoveFriend("someuser");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task GetUserFriends_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.GetUserFriends(Guid.NewGuid());
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetUserFriendRequests_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.GetUserFriendRequests(Guid.NewGuid());
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetUserIsFriend_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.GetUserIsFriend(Guid.NewGuid(), Guid.NewGuid());
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task AcceptFriend_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.AcceptFriend(Guid.NewGuid());
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task DenyFriend_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.DenyFriend(Guid.NewGuid());
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SearchUser_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.SearchUser("test");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SearchUserFriends_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.SearchUserFriends("test");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task IsUsernameAvailable_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.IsUsernameAvailable("newuser");
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task DeleteUser_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.DeleteUser();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task UpdateDetails_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.UpdateDetails("name", "email", "first", "last");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task UpdateEncryptionKey_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.UpdateEncryptionKey("pk", "kid");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task UpdateAvatar_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.UpdateAvatar("base64data");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task UpdateCover_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.UpdateCover("base64data");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SendFeedback_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.SendFeedback("feedback text");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task GetAvatar_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.GetAvatar(Guid.NewGuid());
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetCover_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.GetCover(Guid.NewGuid());
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CloseUserSession_DelegatesToRepository()
    {
        var hub = CreateHub(Guid.NewGuid());
        // Should not throw
        await hub.CloseUserSession();
    }

    // ── ChatHubMessage ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task SendMessage_NullMessage_ReturnsFalse()
    {
        var hub = CreateHub(Guid.NewGuid());
        // CanSendMessageAsync returns false for null
        var result = await hub.SendMessage(null!);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SendMessage_WrongSenderId_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var hub = CreateHub(userId);

        var msg = new Message
        {
            Id = Guid.NewGuid(),
            ChannelId = Guid.NewGuid(),
            SenderId = Guid.NewGuid(), // Different user
            Content = "test"
        };

        var result = await hub.SendMessage(msg);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SendMessage_EmptyContent_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var channelRepo = new StubChannelRepository();
        channelRepo.MembershipResults[channelId] = true;

        var hub = CreateHub(userId, channelRepo: channelRepo);

        var msg = new Message
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            SenderId = userId,
            Content = "",  // empty
        };

        var result = await hub.SendMessage(msg);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SendMessage_UserNotInChannel_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var channelRepo = new StubChannelRepository();
        // Don't add membership

        var hub = CreateHub(userId, channelRepo: channelRepo);

        var msg = new Message
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            SenderId = userId,
            Content = "hello"
        };

        var result = await hub.SendMessage(msg);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SendMessage_DbSaveFails_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var channelRepo = new StubChannelRepository();
        channelRepo.MembershipResults[channelId] = true;

        var messageRepo = new StubMessageRepository { SendMessageResult = false };
        var hub = CreateHub(userId, channelRepo: channelRepo, messageRepo: messageRepo);

        var msg = new Message
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            SenderId = userId,
            Content = "hello"
        };

        var result = await hub.SendMessage(msg);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SendMessage_ValidMessage_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var channelRepo = new StubChannelRepository();
        channelRepo.MembershipResults[channelId] = true;
        channelRepo.ChannelUsers[channelId] = new[] { new UserDto { Id = userId } };

        var tracker = new InMemoryConnectionTracker();
        tracker.TrackConnection(userId, "conn-1");

        var messageRepo = new StubMessageRepository();
        var hub = CreateHub(userId, tracker: tracker, channelRepo: channelRepo, messageRepo: messageRepo);

        var msg = new Message
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            SenderId = userId,
            Content = "hello world"
        };

        var result = await hub.SendMessage(msg);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task SendMessage_WithRecipientContents_DeliversPerUser()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var channelId = Guid.NewGuid();

        var channelRepo = new StubChannelRepository();
        channelRepo.MembershipResults[channelId] = true;
        channelRepo.ChannelUsers[channelId] = new[]
        {
            new UserDto { Id = userId },
            new UserDto { Id = otherUserId }
        };

        var tracker = new InMemoryConnectionTracker();
        tracker.TrackConnection(userId, "conn-sender");
        tracker.TrackConnection(otherUserId, "conn-receiver");

        var hub = CreateHub(userId, tracker: tracker, channelRepo: channelRepo);

        var msg = new Message
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            SenderId = userId,
            RecipientContents = new List<MessageRecipientContent>
            {
                new() { UserId = userId, Content = "encrypted-for-sender" },
                new() { UserId = otherUserId, Content = "encrypted-for-receiver" }
            }
        };

        var result = await hub.SendMessage(msg);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task UpdateMessage_EmptyId_ReturnsFalse()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.UpdateMessage(new Message { Id = Guid.Empty, Content = "test" });
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task UpdateMessage_EmptyContent_ReturnsFalse()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.UpdateMessage(new Message { Id = Guid.NewGuid(), Content = "" });
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SendPendingMessages_UnauthenticatedUser_NoOp()
    {
        var hub = CreateHub(null);
        // Should not throw
        await hub.SendPendingMessages();
    }

    [TestMethod]
    public async Task SendPendingMessages_NoPending_Completes()
    {
        var hub = CreateHub(Guid.NewGuid());
        // stub returns empty list
        await hub.SendPendingMessages();
    }

    [TestMethod]
    public async Task UpdatePendingMessage_UnauthenticatedUser_NoOp()
    {
        var hub = CreateHub(null);
        await hub.UpdatePendingMessage(Guid.NewGuid());
    }

    [TestMethod]
    public async Task UpdatePendingMessage_AuthenticatedUser_Completes()
    {
        var hub = CreateHub(Guid.NewGuid());
        await hub.UpdatePendingMessage(Guid.NewGuid());
    }

    // ── ChatHubMessage (typing) ─────────────────────────────────────────────

    [TestMethod]
    public async Task StartTyping_UnauthenticatedUser_NoOp()
    {
        var hub = CreateHub(null);
        await hub.StartTyping(Guid.NewGuid());
    }

    [TestMethod]
    public async Task StartTyping_NotInChannel_NoOp()
    {
        var hub = CreateHub(Guid.NewGuid());
        // Channel membership is false by default in stub
        await hub.StartTyping(Guid.NewGuid());
    }

    [TestMethod]
    public async Task StopTyping_UnauthenticatedUser_NoOp()
    {
        var hub = CreateHub(null);
        await hub.StopTyping(Guid.NewGuid());
    }

    [TestMethod]
    public async Task StopTyping_NotInChannel_NoOp()
    {
        var hub = CreateHub(Guid.NewGuid());
        await hub.StopTyping(Guid.NewGuid());
    }

    [TestMethod]
    public async Task StartTyping_InChannel_BroadcastsToOtherUsers()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var channelId = Guid.NewGuid();

        var channelRepo = new StubChannelRepository();
        channelRepo.MembershipResults[channelId] = true;
        channelRepo.ChannelUsers[channelId] = new[]
        {
            new UserDto { Id = userId },
            new UserDto { Id = otherUserId }
        };

        var tracker = new InMemoryConnectionTracker();
        tracker.TrackConnection(otherUserId, "conn-other");

        var userRepo = new StubUserRepository
        {
            UserPublicDetailsResults = new[] { new UserDto { Id = userId, Username = "typer" } }
        };

        var hub = CreateHub(userId, tracker: tracker, userRepo: userRepo, channelRepo: channelRepo);

        await hub.StartTyping(channelId);

        var clients = GetClients(hub);
        Assert.IsTrue(clients.ClientCalls.Count > 0);
    }

    // ── ChatHubCall ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task JoinChannelCall_UnauthenticatedUser_NoOp()
    {
        var hub = CreateHub(null);
        await hub.JoinChannelCall(Guid.NewGuid());
    }

    [TestMethod]
    public async Task JoinChannelCall_NotInChannel_NoOp()
    {
        var hub = CreateHub(Guid.NewGuid());
        await hub.JoinChannelCall(Guid.NewGuid());
    }

    [TestMethod]
    public async Task LeaveChannelCall_UnauthenticatedUser_NoOp()
    {
        var hub = CreateHub(null);
        await hub.LeaveChannelCall(Guid.NewGuid());
    }

    [TestMethod]
    public async Task GetChannelCallParticipants_NoActiveCall_ReturnsEmptyArray()
    {
        var hub = CreateHub(Guid.NewGuid());
        var result = await hub.GetChannelCallParticipants(Guid.NewGuid());
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result!.Length);
    }

    [TestMethod]
    public async Task SendOfferToUser_UnauthenticatedUser_NoOp()
    {
        var hub = CreateHub(null);
        await hub.SendOfferToUser(Guid.NewGuid(), Guid.NewGuid(), "sdp");
    }

    [TestMethod]
    public async Task SendAnswerToUser_UnauthenticatedUser_NoOp()
    {
        var hub = CreateHub(null);
        await hub.SendAnswerToUser(Guid.NewGuid(), Guid.NewGuid(), "sdp");
    }

    [TestMethod]
    public async Task SendIceCandidateToUser_UnauthenticatedUser_NoOp()
    {
        var hub = CreateHub(null);
        await hub.SendIceCandidateToUser(Guid.NewGuid(), Guid.NewGuid(), "candidate");
    }
}
