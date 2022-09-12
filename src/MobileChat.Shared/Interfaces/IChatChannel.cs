using MobileChat.Shared.Models;

namespace MobileChat.Shared.Interfaces
{
    public interface IChatChannel
    {
        Task<Channel> CreateChannel(params string[] usernames);
        Task<bool> DeleteChannel(Guid channelId);
        Task<bool> LeaveChannel(Guid channelId);
        Task<bool> AddChannelUsers(Guid channelId, params string[] usernames);
        Task<bool> ChannelContainUser(Guid channelId, Guid userId);
        Task<User[]> GetChannelUsers(Guid channelId);
        Task<Channel[]> GetUserChannels();
        Task<bool> IsChannelAdmin(Guid channelId, Guid userId);
    }
}
