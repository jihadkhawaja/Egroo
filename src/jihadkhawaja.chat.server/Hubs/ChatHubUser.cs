using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : Hub, IUser
    {
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
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

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<UserDto?> GetUserPrivateDetails()
        {
            return _userRepository.GetUserPrivateDetails();
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<string?> GetCurrentUserUsername()
        {
            return _userRepository.GetCurrentUserUsername();
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<bool> AddFriend(string friendusername)
        {
            return _userRepository.AddFriend(friendusername);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<bool> RemoveFriend(string friendusername)
        {
            return _userRepository.RemoveFriend(friendusername);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<UserFriend[]?> GetUserFriends(Guid userId)
        {
            return _userRepository.GetUserFriends(userId);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<UserFriend[]?> GetUserFriendRequests(Guid userId)
        {
            return _userRepository.GetUserFriendRequests(userId);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
        {
            return _userRepository.GetUserIsFriend(userId, friendId);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<bool> AcceptFriend(Guid friendId)
        {
            return _userRepository.AcceptFriend(friendId);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<bool> DenyFriend(Guid friendId)
        {
            return _userRepository.DenyFriend(friendId);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<IEnumerable<UserDto>?> SearchUser(string query, int maxResult = 20)
        {
            return _userRepository.SearchUser(query, maxResult);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<IEnumerable<UserDto>?> SearchUserFriends(string query, int maxResult = 20)
        {
            return _userRepository.SearchUserFriends(query, maxResult);
        }

        [AllowAnonymous]
        public Task<bool> IsUsernameAvailable(string username)
        {
            return _userRepository.IsUsernameAvailable(username);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<bool> DeleteUser()
        {
            return _userRepository.DeleteUser();
        }
        [AllowAnonymous]
        public Task<KeyValuePair<string?, string?>?> GetAvatar(Guid userId)
        {
            return _userRepository.GetAvatar(userId);
        }
        [AllowAnonymous]
        public Task<KeyValuePair<string?, string?>?> GetCover(Guid userId)
        {
            return _userRepository.GetCover(userId);
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<bool> UpdateDetails(string? displayname, string? email, string? firstname, string? lastname)
        {
            return _userRepository.UpdateDetails(displayname, email, firstname, lastname);
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<bool> UpdateAvatar(string? avatarBase64)
        {
            return _userRepository.UpdateAvatar(avatarBase64);
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<bool> UpdateCover(string? coverBase64)
        {
            return _userRepository.UpdateCover(coverBase64);
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<bool> SendFeedback(string text)
        {
            return _userRepository.SendFeedback(text);
        }
    }
}
