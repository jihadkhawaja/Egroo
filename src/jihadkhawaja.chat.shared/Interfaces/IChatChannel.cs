using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    public interface IChatChannel
    {
        Task<Channel?> CreateChannel(params string[] usernames);
        Task<bool> DeleteChannel(Guid channelId);
        Task<bool> LeaveChannel(Guid channelId);
        Task<bool> RemoveChannelUser(Guid channelid, Guid userid);
        Task<bool> AddChannelUsers(Guid channelId, params string[] usernames);
        Task<bool> ChannelContainUser(Guid channelId, Guid userId);
        Task<User[]?> GetChannelUsers(Guid channelId);
        Task<Channel?> GetChannel(Guid channelId);
        Task<Channel[]?> GetUserChannels();
        Task<bool> IsChannelAdmin(Guid channelId, Guid userId);
        Task<Channel[]?> SearchPublicChannels(string searchTerm);
    }
}
