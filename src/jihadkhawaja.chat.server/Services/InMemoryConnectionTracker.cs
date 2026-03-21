using System.Collections.Concurrent;
using jihadkhawaja.chat.shared.Interfaces;

namespace jihadkhawaja.chat.server.Services
{
    /// <summary>
    /// Default in-memory implementation of <see cref="IConnectionTracker"/>.
    /// Registered automatically by <see cref="Register.AddChatHub"/>.
    /// Replace it by registering your own <see cref="IConnectionTracker"/> before calling AddChatHub
    /// (e.g. a Redis-backed tracker for distributed deployments).
    /// </summary>
    public class InMemoryConnectionTracker : IConnectionTracker
    {
        private static readonly ConcurrentDictionary<Guid, HashSet<string>> _userConnections = new();

        public void TrackConnection(Guid userId, string connectionId)
        {
            _userConnections.AddOrUpdate(
                userId,
                new HashSet<string> { connectionId },
                (key, existingConnections) =>
                {
                    lock (existingConnections)
                    {
                        existingConnections.Add(connectionId);
                    }
                    return existingConnections;
                });
        }

        public void UntrackConnection(Guid userId, string connectionId)
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                lock (connections)
                {
                    connections.Remove(connectionId);
                    if (connections.Count == 0)
                    {
                        _userConnections.TryRemove(userId, out _);
                    }
                }
            }
        }

        public bool IsUserOnline(Guid userId)
        {
            return _userConnections.TryGetValue(userId, out var connections) && connections.Count > 0;
        }

        public List<string> GetUserConnectionIds(Guid userId)
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                lock (connections)
                {
                    return connections.ToList();
                }
            }
            return new List<string>();
        }

        public IEnumerable<Guid> GetOnlineUserIds()
        {
            return _userConnections.Keys;
        }

        public int GetConnectionCount(Guid userId)
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                lock (connections)
                {
                    return connections.Count;
                }
            }
            return 0;
        }
    }
}
