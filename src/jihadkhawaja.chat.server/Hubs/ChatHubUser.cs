using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
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
                    var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId.Value);
                    if (user != null)
                    {
                        user.IsOnline = false;
                        user.ConnectionId = null;
                        try
                        {
                            _dbContext.Users.Update(user);
                            await _dbContext.SaveChangesAsync();
                            await NotifyFriendsOfStatusChange(user);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
            }
            Context.Abort();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<User?> GetUserPublicInfo(Guid userId)
        {
            User? user = await _dbContext.Users.Include("UserDetail").FirstOrDefaultAsync(x => x.Id == userId);

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
                UserDetail = user.GetPublicDetail(),
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

            User? user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == ConnectorUserId);

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

                User? currentUser = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == ConnectorUserId);

                if (currentUser == null)
                {
                    return false;
                }

                friendusername = friendusername.ToLower();

                //get friend id from username
                User? friendUser = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == friendusername);
                if (friendUser == null || currentUser.Id == friendUser.Id)
                {
                    return false;
                }

                if (await _dbContext.UsersFriends.FirstOrDefaultAsync(
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

                await _dbContext.UsersFriends.AddAsync(entry);
                await _dbContext.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
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
                User? user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == ConnectorUserId);
                if (user == null)
                {
                    return false;
                }
                //get friend id from username
                User? friendUser = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == friendusername);
                if (friendUser == null)
                {
                    return false;
                }

                if (await _dbContext.UsersFriends.FirstOrDefaultAsync(x => x.UserId == user.Id && x.FriendUserId == friendUser.Id ||
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

                var friendToRemove = await _dbContext.UsersFriends.FirstOrDefaultAsync(x => x.UserId == user.Id && x.FriendUserId == friendUser.Id
                    || x.FriendUserId == user.Id && x.UserId == friendUser.Id);
                if (friendToRemove == null)
                {
                    return false;
                }

                _dbContext.UsersFriends.Remove(friendToRemove);
                await _dbContext.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<UserFriend[]?> GetUserFriends(Guid userId)
        {
            return await _dbContext.UsersFriends.Where(x =>
            (x.UserId == userId && x.DateAcceptedOn != null)
            || (x.FriendUserId == userId && x.DateAcceptedOn != null))
                .ToArrayAsync();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<UserFriend[]?> GetUserFriendRequests(Guid userId)
        {
            return await _dbContext.UsersFriends.Where(x => x.FriendUserId == userId
            && x.DateAcceptedOn == null).ToArrayAsync();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
        {
            UserFriend? result = await _dbContext.UsersFriends.FirstOrDefaultAsync(x => x.UserId == userId
            && x.FriendUserId == friendId && x.DateAcceptedOn != null);

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

            UserFriend? friendRequest = await _dbContext.UsersFriends.FirstOrDefaultAsync(x => x.UserId == friendId
            && x.FriendUserId == ConnectorUserId && x.DateAcceptedOn == null);

            if (friendRequest is null)
            {
                return false;
            }

            friendRequest.DateAcceptedOn = DateTimeOffset.UtcNow;
            try
            {
                _dbContext.UsersFriends.Update(friendRequest);
                await _dbContext.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
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

            try
            {
                var friendRequest = await _dbContext.UsersFriends.FirstOrDefaultAsync(x =>
                x.UserId == friendId && x.FriendUserId == ConnectorUserId && x.DateAcceptedOn == null);
                if (friendRequest == null)
                {
                    return false;
                }

                _dbContext.UsersFriends.Remove(friendRequest);
                await _dbContext.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
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

            User[]? users = await _dbContext.Users.Where(x =>
            x.Username.Contains(query, StringComparison.InvariantCultureIgnoreCase)
            && x.Id != ConnectorUserId)
            .OrderBy(x => x.Username).Take(maxResult).ToArrayAsync();

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
            User[]? friends = await _dbContext.Users.Where(x =>
                 friendIds.Contains(x.Id) &&
                 x.Username.Contains(query, StringComparison.InvariantCultureIgnoreCase))
                .ToArrayAsync();

            if (friends == null)
            {
                return null;
            }

            // Order and limit the results.
            friends = friends.OrderBy(x => x.Username).Take(maxResult).ToArray();

            // Project to a new User object (if needed).
            return friends.Select(x => new User
            {
                Username = x.Username,
                LastLoginDate = x.LastLoginDate,
                IsOnline = x.IsOnline,
                DateCreated = x.DateCreated,
                UserStorage = x.GetAvatar(),
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
            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == username);
            return user is null;
        }

        //delete only the current user's account from request context
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> DeleteUser()
        {
            HttpContext? hc = Context.GetHttpContext();
            if (hc == null)
            {
                return false;
            }

            var identity = hc.User.Identity as ClaimsIdentity;
            var userIdClaim = identity?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return false;
            }

            if (!Guid.TryParse(userIdClaim.Value, out Guid userId))
            {
                return false;
            }

            try
            {
                // Delete user
                var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
                if (user != null)
                {
                    _dbContext.Users.Remove(user);
                }

                // Delete user friends
                var userFriends = await _dbContext.UsersFriends
                    .Where(x => x.UserId == userId || x.FriendUserId == userId)
                    .ToListAsync();
                if (userFriends.Any())
                {
                    _dbContext.UsersFriends.RemoveRange(userFriends);
                }

                // Delete messages sent by the user
                var messages = await _dbContext.Messages
                    .Where(x => x.SenderId == userId)
                    .ToListAsync();
                if (messages.Any())
                {
                    _dbContext.Messages.RemoveRange(messages);
                }

                // Delete pending messages for the user
                var pendingMessages = await _dbContext.UsersPendingMessages
                    .Where(x => x.UserId == userId)
                    .ToListAsync();
                if (pendingMessages.Any())
                {
                    _dbContext.UsersPendingMessages.RemoveRange(pendingMessages);
                }

                // Retrieve channel user records for the user
                var userChannelUsers = await _dbContext.ChannelUsers
                    .Where(x => x.UserId == userId)
                    .ToListAsync();
                if (userChannelUsers.Any())
                {
                    // For each channel, check if the current user is the only participant.
                    foreach (var cu in userChannelUsers)
                    {
                        int channelUserCount = await _dbContext.ChannelUsers
                            .CountAsync(x => x.ChannelId == cu.ChannelId);
                        if (channelUserCount == 1)
                        {
                            var channel = await _dbContext.Channels.FirstOrDefaultAsync(x => x.Id == cu.ChannelId);
                            if (channel != null)
                            {
                                _dbContext.Channels.Remove(channel);
                            }
                        }
                    }
                    // Remove the channel user records for this user.
                    _dbContext.ChannelUsers.RemoveRange(userChannelUsers);
                }

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}