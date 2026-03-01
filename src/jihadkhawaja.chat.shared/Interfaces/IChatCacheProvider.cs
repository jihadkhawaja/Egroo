using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    /// <summary>
    /// Abstracts client-side message caching.
    /// Implement this interface with IndexedDB, SQLite, or any storage backend.
    /// </summary>
    public interface IChatCacheProvider
    {
        Task CacheMessage(Message message);
        Task<Message?> GetCachedMessage(Guid messageId);
        Task<IEnumerable<Message>> GetCachedMessages(Guid channelId);
        Task RemoveCachedMessage(Guid messageId);
        Task ClearCache();
    }
}
