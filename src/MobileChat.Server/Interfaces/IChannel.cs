using MobileChat.Shared.Models;

namespace MobileChat.Server.Interfaces
{
    public interface IChannel
    {
        Task<bool> Create(Channel channel);
        Task<Channel> ReadById(Guid id);
        Task<bool> AddUsers(Guid userid, Guid channelid, params string[] usernames);
        Task<HashSet<User>> GetUsers(Guid channelid);
        Task<HashSet<Channel>> GetUserChannels(Guid userid);
        Task<HashSet<Message>> GetChannelMessages(Guid channelid);
        Task<bool> ContainUser(Guid userid);
    }
}
