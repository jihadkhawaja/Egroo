using MobileChat.Shared.Models;

namespace MobileChat.Server.Interfaces
{
    public interface IChatHub
    {
        Task<KeyValuePair<Guid, bool>> SignUp(string displayname, string username, string email, string password);
        Task<KeyValuePair<Guid, bool>> SignIn(string emailorusername, string password);
        Task<bool> ChangePassword(string emailorusername, string newpassword);
        Task<string> GetUserDisplayName(Guid userId);
        Task<bool> AddFriend(Guid userId, string friendEmailorusername);
        Task<bool> RemoveFriend(Guid userId, string friendEmailorusername);

        Task<Channel> CreateChannel(Guid userId, params string[] usernames);
        Task<User[]> GetChannelUsers(Guid channelid);
        Task<Channel[]> GetUserChannels(Guid userid);

        Task<bool> SendMessage(Message message);
        Task<bool> UpdateMessage(Message message);
        Task<Message[]> ReceiveMessageHistory(Guid channelId);
        Task<Message[]> ReceiveMessageHistoryRange(Guid channelId, int index, int range);
    }
}
