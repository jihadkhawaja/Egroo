using MobileChat.Shared.Models;

namespace MobileChat.Shared.Interfaces
{
    public interface IChatHub
    {
        //users
        Task<KeyValuePair<Guid, bool>> SignUp(string displayname, string username, string email, string password);
        Task<KeyValuePair<Guid, bool>> SignIn(string emailorusername, string password);
        Task<bool> ChangePassword(string emailorusername, string newpassword);
        Task<string> GetUserDisplayName(Guid userId);
        Task<string> GetUserUsername(Guid userId);
        Task<bool> AddFriend(Guid userId, string friendEmailorusername);
        Task<bool> RemoveFriend(Guid userId, string friendEmailorusername);
        Task<UserFriend[]> GetUserFriends(Guid userId);
        Task<UserFriend[]> GetUserFriendRequests(Guid userId);
        Task<bool> GetUserIsFriend(Guid userId, Guid friendId);
        Task<bool> AcceptFriend(Guid userId, Guid friendId);
        Task<bool> DenyFriend(Guid userId, Guid friendId);
        //channels
        Task<Channel> CreateChannel(Guid userId, params string[] usernames);
        Task<bool> DeleteChannel(Guid channelId, Guid userId);
        Task<bool> AddChannelUsers(Guid userId, Guid channelId, params string[] usernames);
        Task<bool> ChannelContainUser(Guid channelId, Guid userId);
        Task<User[]> GetChannelUsers(Guid channelId);
        Task<Channel[]> GetUserChannels(Guid userId);
        Task<bool> IsChannelAdmin(Guid channelId, Guid userId);
        //messages
        Task<bool> SendMessage(Message message);
        Task<bool> UpdateMessage(Message message);
        Task<Message[]> ReceiveMessageHistory(Guid channelId);
        Task<Message[]> ReceiveMessageHistoryRange(Guid channelId, int index, int range);
    }
}
