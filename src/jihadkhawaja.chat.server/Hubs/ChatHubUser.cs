using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : Hub, IUser
    {
        [Authorize]
        public async Task CloseUserSession()
        {
            await _userRepository.CloseUserSession();
            Context.Abort();
        }

        [AllowAnonymous]
        public Task<UserDto?> GetUserPublicDetails(Guid userId)
        {
            return _userRepository.GetUserPublicDetails(userId);
        }

        [Authorize]
        public Task<UserDto?> GetUserPrivateDetails()
        {
            return _userRepository.GetUserPrivateDetails();
        }

        [Authorize]
        public Task<string?> GetCurrentUserUsername()
        {
            return _userRepository.GetCurrentUserUsername();
        }

        [Authorize]
        public Task<bool> AddFriend(string friendusername)
        {
            return _userRepository.AddFriend(friendusername);
        }

        [Authorize]
        public Task<bool> RemoveFriend(string friendusername)
        {
            return _userRepository.RemoveFriend(friendusername);
        }

        [Authorize]
        public Task<UserFriend[]?> GetUserFriends(Guid userId)
        {
            return _userRepository.GetUserFriends(userId);
        }

        [Authorize]
        public Task<UserFriend[]?> GetUserFriendRequests(Guid userId)
        {
            return _userRepository.GetUserFriendRequests(userId);
        }

        [Authorize]
        public Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
        {
            return _userRepository.GetUserIsFriend(userId, friendId);
        }

        [Authorize]
        public Task<bool> AcceptFriend(Guid friendId)
        {
            return _userRepository.AcceptFriend(friendId);
        }

        [Authorize]
        public Task<bool> DenyFriend(Guid friendId)
        {
            return _userRepository.DenyFriend(friendId);
        }

        [Authorize]
        public Task<IEnumerable<UserDto>?> SearchUser(string query, int maxResult = 20)
        {
            return _userRepository.SearchUser(query, maxResult);
        }

        [Authorize]
        public Task<IEnumerable<UserDto>?> SearchUserFriends(string query, int maxResult = 20)
        {
            return _userRepository.SearchUserFriends(query, maxResult);
        }

        [AllowAnonymous]
        public Task<bool> IsUsernameAvailable(string username)
        {
            return _userRepository.IsUsernameAvailable(username);
        }

        [Authorize]
        public Task<bool> DeleteUser()
        {
            return _userRepository.DeleteUser();
        }

        [AllowAnonymous]
        public Task<MediaResult?> GetAvatar(Guid userId)
        {
            return _userRepository.GetAvatar(userId);
        }

        [AllowAnonymous]
        public Task<MediaResult?> GetCover(Guid userId)
        {
            return _userRepository.GetCover(userId);
        }

        [Authorize]
        public Task<bool> UpdateDetails(string? displayname, string? email, string? firstname, string? lastname)
        {
            return _userRepository.UpdateDetails(displayname, email, firstname, lastname);
        }

        [Authorize]
        public Task<bool> UpdateEncryptionKey(string? publicKey, string? keyId)
        {
            return _userRepository.UpdateEncryptionKey(publicKey, keyId);
        }

        [Authorize]
        public Task<bool> AddEncryptionKey(string publicKey, string keyId, string? deviceLabel)
        {
            return _userRepository.AddEncryptionKey(publicKey, keyId, deviceLabel);
        }

        [Authorize]
        public Task<bool> RemoveEncryptionKey(string keyId)
        {
            return _userRepository.RemoveEncryptionKey(keyId);
        }

        [Authorize]
        public Task<UserEncryptionKeyInfo[]?> GetEncryptionKeys()
        {
            return _userRepository.GetEncryptionKeys();
        }

        [Authorize]
        public Task<bool> UpdateAvatar(string? avatarBase64)
        {
            return _userRepository.UpdateAvatar(avatarBase64);
        }

        [Authorize]
        public Task<bool> UpdateCover(string? coverBase64)
        {
            return _userRepository.UpdateCover(coverBase64);
        }

        [Authorize]
        public Task<bool> SendFeedback(string text)
        {
            return _userRepository.SendFeedback(text);
        }
    }
}
