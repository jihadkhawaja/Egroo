using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    public interface IUser
    {
        Task CloseUserSession();
        Task<UserDto?> GetUserPublicDetails(Guid userId);
        Task<UserDto?> GetUserPrivateDetails();
        Task<MediaResult?> GetAvatar(Guid userId);
        Task<MediaResult?> GetCover(Guid userId);
        Task<string?> GetCurrentUserUsername();
        Task<bool> AddFriend(string friendusername);
        Task<bool> RemoveFriend(string friendusername);
        Task<UserFriend[]?> GetUserFriends(Guid userId);
        Task<UserFriend[]?> GetUserFriendRequests(Guid userId);
        Task<bool> GetUserIsFriend(Guid userId, Guid friendId);
        Task<bool> AcceptFriend(Guid friendId);
        Task<bool> DenyFriend(Guid friendId);
        Task<IEnumerable<UserDto>?> SearchUser(string query, int maxResult = 20);
        Task<IEnumerable<UserDto>?> SearchUserFriends(string query, int maxResult = 20);
        Task<bool> IsUsernameAvailable(string username);
        Task<bool> DeleteUser();
        Task<bool> UpdateDetails(string? displayname, string? email, string? firstname, string? lastname);
        Task<bool> UpdateAvatar(string? avatarBase64);
        Task<bool> UpdateCover(string? coverBase64);
        Task<bool> SendFeedback(string text);
    }
}
