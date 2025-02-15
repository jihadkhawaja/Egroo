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
        public async Task CloseUserSession()
        {
            var userId = GetUserIdFromContext();
            if (userId.HasValue)
            {
                if (_userConnections.TryGetValue(userId.Value, out var connections))
                {
                    lock (connections)
                    {
                        connections.Remove(Context.ConnectionId);
                        if (connections.Count == 0)
                        {
                            _userConnections.TryRemove(userId.Value, out _);
                        }
                    }
                }

                if (!_userConnections.ContainsKey(userId.Value))
                {
                    var user = await _userService.ReadFirst(x => x.Id == userId.Value);
                    if (user != null)
                    {
                        user.IsOnline = false;
                        user.ConnectionId = null;
                        await _userService.Update(user);
                        await NotifyFriendsOfStatusChange(user);
                    }
                }
            }
            Context.Abort();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<User?> GetUserPublicInfo(Guid userId)
        {
            User? user = await _userService.ReadFirst(x => x.Id == userId);

            if (user == null)
            {
                return null;
            }
            else if (string.IsNullOrWhiteSpace(user.Username))
            {
                return null;
            }

            User userPublicResult = new()
            {
                AvatarBase64 = user.AvatarBase64,
                Username = user.Username,
                IsOnline = IsUserOnline(user.Id),
                LastLoginDate = user.LastLoginDate,
                DateCreated = user.DateCreated,
            };

            return userPublicResult;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<string?> GetCurrentUserUsername()
        {
            HttpContext? hc = Context.GetHttpContext();

            var identity = hc.User.Identity as ClaimsIdentity;
            var userIdClaim = identity.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

            Guid ConnectorUserId = Guid.Parse(userIdClaim.Value);

            User? user = await _userService.ReadFirst(x => x.Id == ConnectorUserId);

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

                User? currentUser = await _userService.ReadFirst(x => x.Id == ConnectorUserId);

                if (currentUser == null)
                {
                    return false;
                }

                friendusername = friendusername.ToLower();

                //get friend id from username
                User? friendUser = await _userService.ReadFirst(x => x.Username == friendusername);
                if (friendUser == null || currentUser.Id == friendUser.Id)
                {
                    return false;
                }

                if (await _userFriendsService.ReadFirst(
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
                await _userFriendsService.Create(entry);
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
                User? user = await _userService.ReadFirst(x => x.Id == ConnectorUserId);
                if (user == null)
                {
                    return false;
                }
                //get friend id from username
                User? friendUser = await _userService.ReadFirst(x => x.Username == friendusername);
                if (friendUser == null)
                {
                    return false;
                }

                if (await _userFriendsService.ReadFirst(x => x.UserId == user.Id && x.FriendUserId == friendUser.Id ||
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

                await _userFriendsService.Delete(x => x.UserId == user.Id && x.FriendUserId == friendUser.Id
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
            return (await _userFriendsService.Read(x => (x.UserId == userId
            && x.DateAcceptedOn is not null)
            || (x.FriendUserId == userId && x.DateAcceptedOn is not null))).ToArray();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<UserFriend[]?> GetUserFriendRequests(Guid userId)
        {
            return (await _userFriendsService.Read(x => x.FriendUserId == userId
            && x.DateAcceptedOn is null)).ToArray();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
        {
            UserFriend? result = await _userFriendsService.ReadFirst(x => x.UserId == userId
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

            UserFriend? friendRequest = await _userFriendsService.ReadFirst(x => x.UserId == friendId
            && x.FriendUserId == ConnectorUserId && x.DateAcceptedOn is null);

            if (friendRequest is null)
            {
                return false;
            }

            friendRequest.DateAcceptedOn = DateTimeOffset.UtcNow;

            UserFriend[] friendRequests = [friendRequest];
            return await _userFriendsService.Update(friendRequests);
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

            return await _userFriendsService.Delete(x => x.UserId == friendId && x.FriendUserId == ConnectorUserId && x.DateAcceptedOn is null);
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

            IEnumerable<User>? users = (await _userService.Read(x =>
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
                Username = x.Username,
                LastLoginDate = x.LastLoginDate,
                IsOnline = x.IsOnline,
                DateCreated = x.DateCreated,
            });
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IEnumerable<User>?> SearchUserFriends(string query, int maxResult = 20)
        {
            HttpContext? hc = Context.GetHttpContext();
            if (hc == null)
            {
                return null;
            }

            var identity = hc.User.Identity as ClaimsIdentity;
            var userIdClaim = identity?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return null;
            }

            Guid ConnectorUserId = Guid.Parse(userIdClaim.Value);

            // Get the friend records for the current user.
            UserFriend[]? friendRecords = await GetUserFriends(ConnectorUserId);
            if (friendRecords == null || friendRecords.Length == 0)
            {
                return new List<User>();
            }

            // Extract the friend IDs. In each record, the friend is the one that is not the current user.
            var friendIds = friendRecords
                .Select(fr => fr.UserId == ConnectorUserId ? fr.FriendUserId : fr.UserId)
                .Distinct()
                .ToList();

            // Query the user service for users whose IDs are in the friend list
            // and whose username matches the query.
            IEnumerable<User>? friends = await _userService.Read(x =>
                 friendIds.Contains(x.Id) &&
                 x.Username.Contains(query, StringComparison.InvariantCultureIgnoreCase));

            if (friends == null)
            {
                return null;
            }

            // Order and limit the results.
            friends = friends.OrderBy(x => x.Username).Take(maxResult);

            // Project to a new User object (if needed).
            return friends.Select(x => new User
            {
                Username = x.Username,
                LastLoginDate = x.LastLoginDate,
                IsOnline = x.IsOnline,
                DateCreated = x.DateCreated,
                AvatarBase64 = x.AvatarBase64 // include additional fields as needed
            });
        }
        [AllowAnonymous]
        public async Task<bool> IsUsernameAvailable(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            username = username.ToLower();
            var user = await _userService.ReadFirst(x => x.Username == username);
            return user is null;
        }
    }
}