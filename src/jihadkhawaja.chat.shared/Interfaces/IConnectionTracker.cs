namespace jihadkhawaja.chat.shared.Interfaces
{
    /// <summary>
    /// Tracks active SignalR connections per user.
    /// Implement this interface in the host application (e.g., in-memory, Redis, etc.).
    /// </summary>
    public interface IConnectionTracker
    {
        /// <summary>
        /// Record a new connection for a user.
        /// </summary>
        void TrackConnection(Guid userId, string connectionId);

        /// <summary>
        /// Remove a connection for a user.
        /// </summary>
        void UntrackConnection(Guid userId, string connectionId);

        /// <summary>
        /// Check if a user has any active connections.
        /// </summary>
        bool IsUserOnline(Guid userId);

        /// <summary>
        /// Get all connection IDs for a user.
        /// </summary>
        List<string> GetUserConnectionIds(Guid userId);

        /// <summary>
        /// Get all currently online user IDs.
        /// </summary>
        IEnumerable<Guid> GetOnlineUserIds();

        /// <summary>
        /// Get the count of active connections for a user.
        /// </summary>
        int GetConnectionCount(Guid userId);
    }
}
