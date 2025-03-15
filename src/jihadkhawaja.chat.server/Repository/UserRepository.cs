using jihadkhawaja.chat.server.Database;
using jihadkhawaja.chat.server.Hubs;
using jihadkhawaja.chat.server.Models;
using jihadkhawaja.chat.server.Security;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace jihadkhawaja.chat.server.Repository
{
    public class UserRepository : BaseRepository, IUser
    {
        public UserRepository(DataContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            EncryptionService encryptionService)
            : base(dbContext, httpContextAccessor, configuration, encryptionService)
        {
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

        public async Task<UserDto?> GetUserPublicDetails(Guid userId)
        {
            User? user = await _dbContext.Users
                .Include(x => x.UserDetail)
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null || string.IsNullOrWhiteSpace(user.Username))
            {
                return null;
            }

            UserDto userPublicResult = new()
            {
                UserDetail = user.GetPublicDetail(),
                Username = user.Username,
                IsOnline = ChatHub.IsUserOnline(user.Id),
                LastLoginDate = user.LastLoginDate,
                DateCreated = user.DateCreated,
            };

            return userPublicResult;
        }

        public async Task<UserDto?> GetUserPrivateDetails()
        {
            User? user = await _dbContext.Users
                .Include(x => x.UserDetail)
                .FirstOrDefaultAsync(x => x.Id == GetConnectorUserId());

            if (user == null || string.IsNullOrWhiteSpace(user.Username))
            {
                return null;
            }

            UserDto userPrivateResult = new()
            {
                Id = user.Id,
                UserDetail = user.GetPrivateDetail(),
                Username = user.Username,
                IsOnline = ChatHub.IsUserOnline(user.Id),
                LastLoginDate = user.LastLoginDate,
                DateCreated = user.DateCreated,
            };

            return userPrivateResult;
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

            Guid? connectorUserId = GetConnectorUserId();
            if (connectorUserId == null)
            {
                return false;
            }
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

            Guid? connectorUserId = GetConnectorUserId();
            if (connectorUserId == null)
            {
                return false;
            }
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
            Guid? connectorUserId = GetConnectorUserId();
            if (connectorUserId == null)
            {
                return false;
            }

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
            Guid? connectorUserId = GetConnectorUserId();
            if (connectorUserId == null)
            {
                return false;
            }

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

        public async Task<IEnumerable<UserDto>?> SearchUser(string query, int maxResult = 20)
        {
            Guid? connectorUserId = GetConnectorUserId();
            if (connectorUserId == null)
            {
                return null;
            }

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

            return users.Select(x => new UserDto
            {
                Username = x.Username,
                LastLoginDate = x.LastLoginDate,
                IsOnline = x.IsOnline,
                DateCreated = x.DateCreated,
            });
        }

        public async Task<IEnumerable<UserDto>?> SearchUserFriends(string query, int maxResult = 20)
        {
            Guid? connectorUserId = GetConnectorUserId();
            if (!connectorUserId.HasValue)
            {
                return null;
            }

            // Get the friend records for the current user.
            UserFriend[]? friendRecords = await GetUserFriends(connectorUserId.Value);
            if (friendRecords == null || friendRecords.Length == 0)
            {
                return new List<UserDto>();
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

            return friends.Select(x => new UserDto
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
            Guid? connectorUserId = GetConnectorUserId();
            if (connectorUserId == null)
            {
                return false;
            }

            try
            {
                // Delete user
                var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == connectorUserId);
                if (user != null)
                {
                    _dbContext.Users.Remove(user);
                }

                // Delete user friends
                var userFriends = await _dbContext.UsersFriends
                    .Where(x => x.UserId == connectorUserId || x.FriendUserId == connectorUserId)
                    .ToListAsync();
                if (userFriends.Any())
                {
                    _dbContext.UsersFriends.RemoveRange(userFriends);
                }

                // Delete messages sent by the user
                var messages = await _dbContext.Messages
                    .Where(x => x.SenderId == connectorUserId)
                    .ToListAsync();
                if (messages.Any())
                {
                    _dbContext.Messages.RemoveRange(messages);
                }

                // Delete pending messages for the user
                var pendingMessages = await _dbContext.UsersPendingMessages
                    .Where(x => x.UserId == connectorUserId)
                    .ToListAsync();
                if (pendingMessages.Any())
                {
                    _dbContext.UsersPendingMessages.RemoveRange(pendingMessages);
                }

                // Retrieve channel user records for the user
                var userChannelUsers = await _dbContext.ChannelUsers
                    .Where(x => x.UserId == connectorUserId)
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

        public async Task<string?> GetAvatar(Guid userId)
        {
            User? user = await _dbContext.Users
                .Include(x => x.UserStorage)
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null || string.IsNullOrWhiteSpace(user.Username))
            {
                return null;
            }

            return user.UserStorage?.AvatarImageBase64;
        }

        public async Task<string?> GetCover(Guid userId)
        {
            User? user = await _dbContext.Users
                .Include(x => x.UserStorage)
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user == null || string.IsNullOrWhiteSpace(user.Username))
            {
                return null;
            }

            return user.UserStorage?.CoverImageBase64;
        }

        public async Task<bool> UpdateDetails(string? displayname, string? email, string? firstname, string? lastname)
        {
            User? user = await GetConnectedUserWithDetails();
            if (user is null)
            {
                return false;
            }

            if (user.UserDetail is null)
            {
                user.UserDetail = new UserDetail();
            }

            if (!string.IsNullOrWhiteSpace(displayname))
            {
                user.UserDetail.DisplayName = displayname;
            }
            if (!string.IsNullOrWhiteSpace(email))
            {
                user.UserDetail.Email = email;
            }
            if (!string.IsNullOrWhiteSpace(firstname))
            {
                user.UserDetail.FirstName = firstname;
            }
            if (!string.IsNullOrWhiteSpace(lastname))
            {
                user.UserDetail.LastName = lastname;
            }

            try
            {
                _dbContext.Users.Update(user);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateAvatar(string? avatarBase64)
        {
            User? user = await GetConnectedUserWithStorage();
            if (user is null)
            {
                return false;
            }

            if (user.UserStorage is null)
            {
                user.UserStorage = new UserStorage();
            }

            user.UserStorage.AvatarImageBase64 = avatarBase64;

            try
            {
                _dbContext.Users.Update(user);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateCover(string? coverBase64)
        {
            User? user = await GetConnectedUserWithStorage();
            if (user == null)
            {
                return false;
            }

            if (user.UserStorage is null)
            {
                user.UserStorage = new UserStorage();
            }

            user.UserStorage.CoverImageBase64 = coverBase64;

            try
            {
                _dbContext.Users.Update(user);
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
