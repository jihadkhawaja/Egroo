using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : IChatUser
    {
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<string?> GetUserUsername(Guid userId)
        {
            User? user = await UserService.ReadFirst(x => x.Id == userId);

            if (user == null)
            {
                return null;
            }
            else if (string.IsNullOrWhiteSpace(user.Username))
            {
                return null;
            }

            string username = user.Username;

            return username;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<string?> GetCurrentUserUsername()
        {
            HttpContext? hc = Context.GetHttpContext();

            var identity = hc.User.Identity as ClaimsIdentity;
            var userIdClaim = identity.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

            Guid ConnectorUserId = Guid.Parse(userIdClaim.Value);

            User? user = await UserService.ReadFirst(x => x.Id == ConnectorUserId);

            if (user == null)
            {
                return null;
            }
            else if (string.IsNullOrWhiteSpace(user.Username))
            {
                return null;
            }

            string username = user.Username;

            return username;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> AddFriend(string friendusername)
        {
            if (string.IsNullOrEmpty(friendusername))
            {
                return false;
            }

            try
            {
                HttpContext? hc = Context.GetHttpContext();

                if (hc == null)
                {
                    return false;
                }

                var identity = hc.User.Identity as ClaimsIdentity;
                var userIdClaim = identity.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    return false;
                }

                Guid ConnectorUserId = Guid.Parse(userIdClaim.Value);

                User? currentUser = await UserService.ReadFirst(x => x.Id == ConnectorUserId);

                if (currentUser == null)
                {
                    return false;
                }

                friendusername = friendusername.ToLower();

                //get friend id from username
                User? friendUser = await UserService.ReadFirst(x => x.Username == friendusername);
                if (friendUser == null || currentUser.Id == friendUser.Id)
                {
                    return false;
                }

                if (await UserFriendsService.ReadFirst(
                    x => x.UserId == currentUser.Id && x.FriendUserId == friendUser.Id
                || x.FriendUserId == currentUser.Id && x.UserId == friendUser.Id) != null)
                {
                    return false;
                }

                UserFriend entry = new()
                {
                    Id = Guid.NewGuid(),
                    UserId = currentUser.Id,
                    FriendUserId = friendUser.Id,
                    DateCreated = DateTime.UtcNow
                };
                await UserFriendsService.Create(entry);
            }
            catch
            {
                return false;
            }

            return true;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> RemoveFriend(string friendusername)
        {
            if (string.IsNullOrEmpty(friendusername))
            {
                return false;
            }

            try
            {
                HttpContext? hc = Context.GetHttpContext();
                if (hc == null)
                {
                    return false;
                }

                var identity = hc.User.Identity as ClaimsIdentity;
                var userIdClaim = identity.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    return false;
                }

                Guid ConnectorUserId = Guid.Parse(userIdClaim.Value);
                friendusername = friendusername.ToLower();

                //get user id from username
                User? user = await UserService.ReadFirst(x => x.Id == ConnectorUserId);
                if (user == null)
                {
                    return false;
                }
                //get friend id from username
                User? friendUser = await UserService.ReadFirst(x => x.Username == friendusername);
                if (friendUser == null)
                {
                    return false;
                }

                if (await UserFriendsService.ReadFirst(x => x.UserId == user.Id && x.FriendUserId == friendUser.Id ||
                x.FriendUserId == user.Id && x.UserId == friendUser.Id) == null)
                {
                    return false;
                }

                UserFriend entry = new()
                {
                    UserId = user.Id,
                    FriendUserId = friendUser.Id,
                    DateCreated = DateTime.UtcNow
                };

                await UserFriendsService.Delete(x => x.UserId == user.Id && x.FriendUserId == friendUser.Id
                || x.FriendUserId == user.Id && x.UserId == friendUser.Id);
            }
            catch
            {
                return false;
            }

            return true;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<UserFriend[]?> GetUserFriends(Guid userId)
        {
            return (await UserFriendsService.Read(x => (x.UserId == userId
            && x.DateAcceptedOn is not null)
            || (x.FriendUserId == userId && x.DateAcceptedOn is not null))).ToArray();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<UserFriend[]?> GetUserFriendRequests(Guid userId)
        {
            return (await UserFriendsService.Read(x => x.FriendUserId == userId
            && x.DateAcceptedOn is null)).ToArray();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
        {
            UserFriend? result = await UserFriendsService.ReadFirst(x => x.UserId == userId
            && x.FriendUserId == friendId && x.DateAcceptedOn is not null);

            if (result is null)
            {
                return false;
            }

            return true;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> AcceptFriend(Guid friendId)
        {
            HttpContext? hc = Context.GetHttpContext();
            if (hc == null)
            {
                return false;
            }

            var identity = hc.User.Identity as ClaimsIdentity;
            var userIdClaim = identity.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return false;
            }

            Guid ConnectorUserId = Guid.Parse(userIdClaim.Value);

            UserFriend? friendRequest = await UserFriendsService.ReadFirst(x => x.UserId == friendId
            && x.FriendUserId == ConnectorUserId && x.DateAcceptedOn is null);

            if (friendRequest is null)
            {
                return false;
            }

            friendRequest.DateAcceptedOn = DateTimeOffset.UtcNow;

            UserFriend[] friendRequests = [friendRequest];
            return await UserFriendsService.Update(friendRequests);
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> DenyFriend(Guid friendId)
        {
            HttpContext? hc = Context.GetHttpContext();
            if (hc == null)
            {
                return false;
            }

            var identity = hc.User.Identity as ClaimsIdentity;
            var userIdClaim = identity.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return false;
            }

            Guid ConnectorUserId = Guid.Parse(userIdClaim.Value);

            return await UserFriendsService.Delete(x => x.UserId == friendId && x.FriendUserId == ConnectorUserId && x.DateAcceptedOn is null);
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IEnumerable<User>?> SearchUser(string query, int maxResult = 20)
        {
            HttpContext? hc = Context.GetHttpContext();
            if (hc == null)
            {
                return null;
            }

            var identity = hc.User.Identity as ClaimsIdentity;
            var userIdClaim = identity.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return null;
            }

            Guid ConnectorUserId = Guid.Parse(userIdClaim.Value);

            IEnumerable<User>? users = (await UserService.Read(x =>
            x.Username.Contains(query, StringComparison.InvariantCultureIgnoreCase)
            && x.Id != ConnectorUserId))
            .OrderBy(x => x.Username).Take(maxResult);

            if (users == null)
            {
                return null;
            }

            return users.Select(x =>
            new User
            {
                Username = x.Username
            });
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> IsUserOnline(Guid userId)
        {
            User? user = await UserService.ReadFirst(x => x.Id == userId);

            if (user == null)
            {
                return false;
            }
            else if (string.IsNullOrWhiteSpace(user.Username))
            {
                return false;
            }

            return user.IsOnline;
        }
    }
}