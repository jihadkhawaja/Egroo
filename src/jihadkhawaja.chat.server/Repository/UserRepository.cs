using jihadkhawaja.chat.server.Database;
using jihadkhawaja.chat.server.Hubs;
using jihadkhawaja.chat.server.Security;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace jihadkhawaja.chat.server.Repository
{
    public class UserRepository : BaseRepository, IUser
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserRepository(DataContext dbContext, 
            IConfiguration configuration, 
            EncryptionService encryptionService,
            IHttpContextAccessor httpContextAccessor)
            : base(dbContext, configuration, encryptionService)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // Stub for notifying friends about status change.
        private async Task NotifyFriendsOfStatusChange(User user)
        {
            // Replace with your notification logic if needed.
            await Task.CompletedTask;
        }

        public async Task CloseUserSession()
        {
            var userId = GetConnectorUserId();
            if (userId.HasValue)
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
                        // Ignored
                    }
                }
            }
        }

        private Guid? GetConnectorUserId()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out Guid userId))
                {
                    return userId;
                }
            }
            else
            {
                string token = httpContext?.Request.Query["access_token"] ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var jwtToken = tokenHandler.ReadJwtToken(token);
                    var claim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                    if (claim != null && Guid.TryParse(claim.Value, out Guid userId))
                    {
                        return userId;
                    }
                }
            }
            return null;
        }

        public async Task<User?> GetUserPublicInfo(Guid userId)
        {
            User? user = await _dbContext.Users
                .Include(x => x.UserDetail)
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null || string.IsNullOrWhiteSpace(user.Username))
            {
                return null;
            }

            User userPublicResult = new()
            {
                UserDetail = user.GetPublicDetail(),
                Username = user.Username,
                IsOnline = ChatHub.IsUserOnline(user.Id),
                LastLoginDate = user.LastLoginDate,
                DateCreated = user.DateCreated,
            };

            return userPublicResult;
        }

        public async Task<string?> GetCurrentUserUsername()
        {
            var userId = GetConnectorUserId();
            if (userId is null)
            {
                return null;
            }

            User? user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId.Value);
            if (user == null || string.IsNullOrWhiteSpace(user.Username))
            {
                return null;
            }

            return user.Username;
        }

        public async Task<bool> AddFriend(string friendusername)
        {
            if (string.IsNullOrEmpty(friendusername))
            {
                return false;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return false;
            }

            var identity = httpContext.User.Identity as ClaimsIdentity;
            var userIdClaim = identity?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return false;
            }

            Guid connectorUserId = Guid.Parse(userIdClaim.Value);
            User? currentUser = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == connectorUserId);
            if (currentUser == null)
            {
                return false;
            }

            friendusername = friendusername.ToLower();

            // Get friend id from username
            User? friendUser = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == friendusername);
            if (friendUser == null || currentUser.Id == friendUser.Id)
            {
                return false;
            }

            if (await _dbContext.UsersFriends.FirstOrDefaultAsync(x =>
                    (x.UserId == currentUser.Id && x.FriendUserId == friendUser.Id) ||
                    (x.FriendUserId == currentUser.Id && x.UserId == friendUser.Id)) != null)
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

        public async Task<bool> RemoveFriend(string friendusername)
        {
            if (string.IsNullOrEmpty(friendusername))
            {
                return false;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return false;
            }

            var identity = httpContext.User.Identity as ClaimsIdentity;
            var userIdClaim = identity?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return false;
            }

            Guid connectorUserId = Guid.Parse(userIdClaim.Value);
            friendusername = friendusername.ToLower();

            // Get user id from username
            User? user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == connectorUserId);
            if (user == null)
            {
                return false;
            }
            // Get friend id from username
            User? friendUser = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == friendusername);
            if (friendUser == null)
            {
                return false;
            }

            var friendToRemove = await _dbContext.UsersFriends.FirstOrDefaultAsync(x =>
                (x.UserId == user.Id && x.FriendUserId == friendUser.Id) ||
                (x.FriendUserId == user.Id && x.UserId == friendUser.Id));
            if (friendToRemove == null)
            {
                return false;
            }

            _dbContext.UsersFriends.Remove(friendToRemove);
            await _dbContext.SaveChangesAsync();

            return true;
        }

        public async Task<UserFriend[]?> GetUserFriends(Guid userId)
        {
            return await _dbContext.UsersFriends.Where(x =>
                (x.UserId == userId && x.DateAcceptedOn != null) ||
                (x.FriendUserId == userId && x.DateAcceptedOn != null))
                .ToArrayAsync();
        }

        public async Task<UserFriend[]?> GetUserFriendRequests(Guid userId)
        {
            return await _dbContext.UsersFriends.Where(x => x.FriendUserId == userId && x.DateAcceptedOn == null)
                .ToArrayAsync();
        }

        public async Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
        {
            UserFriend? result = await _dbContext.UsersFriends.FirstOrDefaultAsync(x =>
                x.UserId == userId && x.FriendUserId == friendId && x.DateAcceptedOn != null);

            return result != null;
        }

        public async Task<bool> AcceptFriend(Guid friendId)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return false;
            }

            var identity = httpContext.User.Identity as ClaimsIdentity;
            var userIdClaim = identity?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return false;
            }

            Guid connectorUserId = Guid.Parse(userIdClaim.Value);

            UserFriend? friendRequest = await _dbContext.UsersFriends.FirstOrDefaultAsync(x =>
                x.UserId == friendId && x.FriendUserId == connectorUserId && x.DateAcceptedOn == null);

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

        public async Task<bool> DenyFriend(Guid friendId)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return false;
            }

            var identity = httpContext.User.Identity as ClaimsIdentity;
            var userIdClaim = identity?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return false;
            }

            Guid connectorUserId = Guid.Parse(userIdClaim.Value);

            try
            {
                var friendRequest = await _dbContext.UsersFriends.FirstOrDefaultAsync(x =>
                    x.UserId == friendId && x.FriendUserId == connectorUserId && x.DateAcceptedOn == null);
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

        public async Task<IEnumerable<User>?> SearchUser(string query, int maxResult = 20)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            var identity = httpContext.User.Identity as ClaimsIdentity;
            var userIdClaim = identity?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return null;
            }

            Guid connectorUserId = Guid.Parse(userIdClaim.Value);

            User[]? users = await _dbContext.Users.Where(x =>
                x.Username.Contains(query, StringComparison.InvariantCultureIgnoreCase) &&
                x.Id != connectorUserId)
                .OrderBy(x => x.Username)
                .Take(maxResult)
                .ToArrayAsync();

            if (users == null)
            {
                return null;
            }

            return users.Select(x => new User
            {
                Username = x.Username,
                LastLoginDate = x.LastLoginDate,
                IsOnline = x.IsOnline,
                DateCreated = x.DateCreated,
            });
        }

        public async Task<IEnumerable<User>?> SearchUserFriends(string query, int maxResult = 20)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            var identity = httpContext.User.Identity as ClaimsIdentity;
            var userIdClaim = identity?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return null;
            }

            Guid connectorUserId = Guid.Parse(userIdClaim.Value);

            // Get the friend records for the current user.
            UserFriend[]? friendRecords = await GetUserFriends(connectorUserId);
            if (friendRecords == null || friendRecords.Length == 0)
            {
                return new List<User>();
            }

            // Extract the friend IDs.
            var friendIds = friendRecords
                .Select(fr => fr.UserId == connectorUserId ? fr.FriendUserId : fr.UserId)
                .Distinct()
                .ToList();

            // Query for friends whose username matches the query.
            User[]? friends = await _dbContext.Users.Where(x =>
                 friendIds.Contains(x.Id) &&
                 x.Username.Contains(query, StringComparison.InvariantCultureIgnoreCase))
                .ToArrayAsync();

            if (friends == null)
            {
                return null;
            }

            friends = friends.OrderBy(x => x.Username).Take(maxResult).ToArray();

            return friends.Select(x => new User
            {
                Username = x.Username,
                LastLoginDate = x.LastLoginDate,
                IsOnline = x.IsOnline,
                DateCreated = x.DateCreated,
                UserStorage = x.GetAvatar(),
            });
        }

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

        public async Task<bool> DeleteUser()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return false;
            }

            var identity = httpContext.User.Identity as ClaimsIdentity;
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
