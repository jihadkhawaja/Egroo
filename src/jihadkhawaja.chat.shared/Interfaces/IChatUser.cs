using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    public interface IChatUser
    {
        Task CloseUserSession();
        Task<User?> GetUserPublicInfo(Guid userId);
        Task<string?> GetCurrentUserUsername();
        Task<bool> AddFriend(string friendusername);
        Task<bool> RemoveFriend(string friendusername);
        Task<UserFriend[]?> GetUserFriends(Guid userId);
        Task<UserFriend[]?> GetUserFriendRequests(Guid userId);
        Task<bool> GetUserIsFriend(Guid userId, Guid friendId);
        Task<bool> AcceptFriend(Guid friendId);
        Task<bool> DenyFriend(Guid friendId);
        Task<IEnumerable<User>?> SearchUser(string query, int maxResult = 20);
        Task<IEnumerable<User>?> SearchUserFriends(string query, int maxResult = 20);
        Task<bool> IsUsernameAvailable(string username);
        Task<bool> DeleteUser();
    }
}
