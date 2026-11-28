using Egroo.Contracts.Models;

namespace Egroo.Contracts.Interfaces
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
        Task<bool> UpdateEncryptionKey(string? publicKey, string? keyId);
        Task<bool> AddEncryptionKey(string publicKey, string keyId, string? deviceLabel);
        Task<bool> RemoveEncryptionKey(string keyId);
        Task<UserEncryptionKeyInfo[]?> GetEncryptionKeys();
        /// <summary>Sets the current user's avatar from base64 image bytes. Pass null to remove it.</summary>
        Task<bool> UpdateAvatar(string? avatarBase64);
        /// <summary>Sets the current user's cover from base64 image bytes. Pass null to remove it.</summary>
        Task<bool> UpdateCover(string? coverBase64);
        Task<bool> SendFeedback(string text);
    }
}
