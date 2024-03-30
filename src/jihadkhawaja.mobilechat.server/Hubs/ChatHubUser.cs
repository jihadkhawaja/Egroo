using jihadkhawaja.mobilechat.server.Helpers;
using jihadkhawaja.mobilechat.server.Interfaces;
using jihadkhawaja.mobilechat.server.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace jihadkhawaja.mobilechat.server.Hubs
{
    public partial class ChatHub : IChatUser
    {
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<string?> GetUserDisplayName(Guid userId)
        {
            User? user = await UserService.ReadFirst(x => x.Id == userId);

            if (user == null)
            {
                return null;
            }
            else if (string.IsNullOrWhiteSpace(user.DisplayName))
            {
                return null;
            }

            string displayname = user.DisplayName;

            return displayname;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<string?> GetCurrentUserDisplayName()
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
            else if (string.IsNullOrWhiteSpace(user.DisplayName))
            {
                return null;
            }

            string displayname = user.DisplayName;

            return displayname;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<string?> GetUserDisplayNameByEmail(string email)
        {
            email = email.ToLower();
            User? user = await UserService.ReadFirst(x => x.Email == email);

            if (user == null)
            {
                return null;
            }
            else if (string.IsNullOrWhiteSpace(user.DisplayName))
            {
                return null;
            }

            string displayname = user.DisplayName;

            return displayname;
        }
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
        public async Task<bool> AddFriend(string friendEmailorusername)
        {
            if (string.IsNullOrEmpty(friendEmailorusername))
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

                friendEmailorusername = friendEmailorusername.ToLower();

                if (PatternMatchHelper.IsValidEmail(friendEmailorusername))
                {
                    //get friend id from email
                    User? friendUser = await UserService.ReadFirst(x => x.Email == friendEmailorusername);
                    if (friendUser == null || currentUser.Id == friendUser.Id)
                    {
                        return false;
                    }

                    if (await UserFriendsService.ReadFirst(x => x.UserId == currentUser.Id && x.FriendUserId == friendUser.Id
                    || x.FriendUserId == currentUser.Id && x.UserId == friendUser.Id) != null)
                    {
                        return false;
                    }

                    UserFriend entry = new() { UserId = currentUser.Id, FriendUserId = friendUser.Id, DateCreated = DateTime.UtcNow };
                    UserFriend[] entries = new UserFriend[1] { entry };
                    await UserFriendsService.Create(entries);
                }
                else
                {
                    //get friend id from username
                    User? friendUser = await UserService.ReadFirst(x => x.Username == friendEmailorusername);
                    if (friendUser == null || currentUser.Id == friendUser.Id)
                    {
                        return false;
                    }

                    if (await UserFriendsService.ReadFirst(x => x.UserId == currentUser.Id && x.FriendUserId == friendUser.Id
                    || x.FriendUserId == currentUser.Id && x.UserId == friendUser.Id) != null)
                    {
                        return false;
                    }

                    UserFriend entry = new() { Id = Guid.NewGuid(), UserId = currentUser.Id, FriendUserId = friendUser.Id, DateCreated = DateTime.UtcNow };
                    UserFriend[] entries = new UserFriend[1] { entry };
                    await UserFriendsService.Create(entries);
                }
            }
            catch
            {
                return false;
            }

            return true;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> RemoveFriend(string friendEmailorusername)
        {
            if (string.IsNullOrEmpty(friendEmailorusername))
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
                friendEmailorusername = friendEmailorusername.ToLower();

                if (PatternMatchHelper.IsValidEmail(friendEmailorusername))
                {
                    //get user id from email
                    User? user = await UserService.ReadFirst(x => x.Id == ConnectorUserId);
                    if (user == null)
                    {
                        return false;
                    }
                    //get friend id from email
                    User? friendUser = await UserService.ReadFirst(x => x.Email == friendEmailorusername);
                    if (friendUser == null)
                    {
                        return false;
                    }

                    if (await UserFriendsService.ReadFirst(x => x.UserId == user.Id && x.FriendUserId == friendUser.Id ||
                    x.FriendUserId == user.Id && x.UserId == friendUser.Id) != null)
                    {
                        return false;
                    }

                    UserFriend entry = new() { UserId = user.Id, FriendUserId = friendUser.Id, DateCreated = DateTime.UtcNow };

                    await UserFriendsService.Delete(x => x.Id == entry.Id);
                }
                else
                {
                    //get user id from username
                    User? user = await UserService.ReadFirst(x => x.Id == ConnectorUserId);
                    if (user == null)
                    {
                        return false;
                    }
                    //get friend id from username
                    User? friendUser = await UserService.ReadFirst(x => x.Username == friendEmailorusername);
                    if (friendUser == null)
                    {
                        return false;
                    }

                    if (await UserFriendsService.ReadFirst(x => x.UserId == user.Id && x.FriendUserId == friendUser.Id ||
                    x.FriendUserId == user.Id && x.UserId == friendUser.Id) == null)
                    {
                        return false;
                    }

                    UserFriend entry = new() { UserId = user.Id, FriendUserId = friendUser.Id, DateCreated = DateTime.UtcNow };

                    await UserFriendsService.Delete(x => x.UserId == user.Id && x.FriendUserId == friendUser.Id
                    || x.FriendUserId == user.Id && x.UserId == friendUser.Id);
                }
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
            return (await UserFriendsService.Read(x => (x.UserId == userId && x.IsAccepted) || (x.FriendUserId == userId && x.IsAccepted))).ToArray();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<UserFriend[]?> GetUserFriendRequests(Guid userId)
        {
            return (await UserFriendsService.Read(x => x.FriendUserId == userId && !x.IsAccepted)).ToArray();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
        {
            UserFriend? result = await UserFriendsService.ReadFirst(x => x.UserId == userId && x.FriendUserId == friendId && x.IsAccepted);

            if (result is null)
            {
                return false;
            }

            return result.IsAccepted;
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
            var userIdClaim = identity.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return false;
            }

            Guid ConnectorUserId = Guid.Parse(userIdClaim.Value);

            UserFriend? friendRequest = await UserFriendsService.ReadFirst(x => x.UserId == friendId && x.FriendUserId == ConnectorUserId && !x.IsAccepted);

            if (friendRequest is null)
            {
                return false;
            }

            friendRequest.IsAccepted = true;

            UserFriend[] friendRequests = new UserFriend[1] { friendRequest };
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

            return await UserFriendsService.Delete(x => x.UserId == friendId && x.FriendUserId == ConnectorUserId && !x.IsAccepted);
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
            (x.Username.Contains(query, StringComparison.InvariantCultureIgnoreCase)
            || x.DisplayName.Contains(query, StringComparison.InvariantCultureIgnoreCase))
            && x.Id != ConnectorUserId))
            .OrderBy(x => x.Username).Take(maxResult);

            if (users == null)
            {
                return null;
            }

            return users.Select(x =>
            new User
            {
                DisplayName = x.DisplayName,
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