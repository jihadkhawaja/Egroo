using MobileChat.Shared.Models;

namespace MobileChat.Shared.Interfaces
{
    public interface IChatChannel
    {
        Task<Channel> CreateChannel(Guid userId, params string[] usernames);
        Task<bool> DeleteChannel(Guid channelId, Guid userId);
        Task<bool> LeaveChannel(Guid userId, Guid channelId);
        Task<bool> AddChannelUsers(Guid userId, Guid channelId, params string[] usernames);
        Task<bool> ChannelContainUser(Guid channelId, Guid userId);
        Task<User[]> GetChannelUsers(Guid channelId);
        Task<Channel[]> GetUserChannels(Guid userId);
        Task<bool> IsChannelAdmin(Guid channelId, Guid userId);
    }
}
