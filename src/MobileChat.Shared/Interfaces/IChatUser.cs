using MobileChat.Shared.Models;

namespace MobileChat.Shared.Interfaces
{
    public interface IChatUser
    {
        Task<string> GetUserDisplayName(Guid userId);
        Task<string> GetUserUsername(Guid userId);
        Task<bool> AddFriend(Guid userId, string friendEmailorusername);
        Task<bool> RemoveFriend(Guid userId, string friendEmailorusername);
        Task<UserFriend[]> GetUserFriends(Guid userId);
        Task<UserFriend[]> GetUserFriendRequests(Guid userId);
        Task<bool> GetUserIsFriend(Guid userId, Guid friendId);
        Task<bool> AcceptFriend(Guid userId, Guid friendId);
        Task<bool> DenyFriend(Guid userId, Guid friendId);
    }
}
