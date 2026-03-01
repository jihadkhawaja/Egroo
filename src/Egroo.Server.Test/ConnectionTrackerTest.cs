using jihadkhawaja.chat.server.Services;

namespace Egroo.Server.Test
{
    /// <summary>
    /// Tests <see cref="InMemoryConnectionTracker"/> from the
    /// <c>jihadkhawaja.chat.server</c> NuGet library.
    /// </summary>
    [TestClass]
    public class ConnectionTrackerTest
    {
        private IConnectionTracker _tracker = null!;

        [TestInitialize]
        public void Initialize()
        {
            _tracker = new InMemoryConnectionTracker();
        }

        [TestMethod]
        public void TrackConnection_NewUser_IsOnline()
        {
            var userId = Guid.NewGuid();
            _tracker.TrackConnection(userId, "conn-1");

            Assert.IsTrue(_tracker.IsUserOnline(userId));
        }

        [TestMethod]
        public void UntrackConnection_LastConnection_UserGoesOffline()
        {
            var userId = Guid.NewGuid();
            _tracker.TrackConnection(userId, "conn-1");
            _tracker.UntrackConnection(userId, "conn-1");

            Assert.IsFalse(_tracker.IsUserOnline(userId));
        }

        [TestMethod]
        public void UntrackConnection_OneOfMany_UserRemainsOnline()
        {
            var userId = Guid.NewGuid();
            _tracker.TrackConnection(userId, "conn-1");
            _tracker.TrackConnection(userId, "conn-2");
            _tracker.UntrackConnection(userId, "conn-1");

            Assert.IsTrue(_tracker.IsUserOnline(userId), "User should remain online while one connection exists.");
        }

        [TestMethod]
        public void GetConnectionCount_MultipleConnections_ReturnsCorrectCount()
        {
            var userId = Guid.NewGuid();
            _tracker.TrackConnection(userId, "conn-1");
            _tracker.TrackConnection(userId, "conn-2");
            _tracker.TrackConnection(userId, "conn-3");

            Assert.AreEqual(3, _tracker.GetConnectionCount(userId));
        }

        [TestMethod]
        public void GetUserConnectionIds_ReturnsAllTrackedIds()
        {
            var userId = Guid.NewGuid();
            _tracker.TrackConnection(userId, "conn-a");
            _tracker.TrackConnection(userId, "conn-b");

            var ids = _tracker.GetUserConnectionIds(userId);
            CollectionAssert.Contains(ids, "conn-a");
            CollectionAssert.Contains(ids, "conn-b");
            Assert.AreEqual(2, ids.Count);
        }

        [TestMethod]
        public void GetOnlineUserIds_ReturnsOnlyOnlineUsers()
        {
            var userA = Guid.NewGuid();
            var userB = Guid.NewGuid();
            var userC = Guid.NewGuid();

            _tracker.TrackConnection(userA, "a-conn-1");
            _tracker.TrackConnection(userB, "b-conn-1");
            // userC never connects

            var online = _tracker.GetOnlineUserIds().ToList();
            CollectionAssert.Contains(online, userA);
            CollectionAssert.Contains(online, userB);
            CollectionAssert.DoesNotContain(online, userC);
        }

        [TestMethod]
        public void IsUserOnline_UnknownUser_ReturnsFalse()
        {
            Assert.IsFalse(_tracker.IsUserOnline(Guid.NewGuid()));
        }

        [TestMethod]
        public void GetConnectionCount_UnknownUser_ReturnsZero()
        {
            Assert.AreEqual(0, _tracker.GetConnectionCount(Guid.NewGuid()));
        }

        [TestMethod]
        public void GetUserConnectionIds_UnknownUser_ReturnsEmptyList()
        {
            var ids = _tracker.GetUserConnectionIds(Guid.NewGuid());
            Assert.IsNotNull(ids);
            Assert.AreEqual(0, ids.Count);
        }
    }
}
