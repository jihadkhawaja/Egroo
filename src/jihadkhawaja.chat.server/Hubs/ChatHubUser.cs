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

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<User?> GetUserPublicInfo(Guid userId)
        {
            return _userRepository.GetUserPublicInfo(userId);
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
        public Task<IEnumerable<User>?> SearchUser(string query, int maxResult = 20)
        {
            return _userRepository.SearchUser(query, maxResult);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<IEnumerable<User>?> SearchUserFriends(string query, int maxResult = 20)
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
    }
}
