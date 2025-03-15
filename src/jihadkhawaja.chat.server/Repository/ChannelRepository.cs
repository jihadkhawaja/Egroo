using jihadkhawaja.chat.server.Database;
using jihadkhawaja.chat.server.Hubs;
using jihadkhawaja.chat.server.Security;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace jihadkhawaja.chat.server.Repository
{
    public class ChannelRepository : BaseRepository, IChannel
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ChannelRepository(DataContext dbContext, 
            IConfiguration configuration, 
            EncryptionService encryptionService,
            IHttpContextAccessor httpContextAccessor)
            : base(dbContext, configuration, encryptionService)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private async Task<User?> GetConnectedUser()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            var identity = httpContext.User.Identity as ClaimsIdentity;
            var claim = identity?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            if (claim == null || !Guid.TryParse(claim.Value, out Guid userId))
            {
                return null;
            }

            return await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        }

        public async Task<Channel?> CreateChannel(params string[] usernames)
        {
            if (usernames.Length == 0)
            {
                return null;
            }

            var channel = new Channel
            {
                Id = Guid.NewGuid(),
                DateCreated = DateTime.UtcNow,
            };

            try
            {
                await _dbContext.AddAsync(channel);
                await _dbContext.SaveChangesAsync();
            }
            catch
            {
                return null;
            }

            // Note: Adding users is delegated to AddChannelUsers (notification will be handled in the hub).
            await AddChannelUsers(channel.Id, usernames);

            return channel;
        }

        public async Task<bool> AddChannelUsers(Guid channelId, params string[] usernames)
        {
            try
            {
                var connectedUser = await GetConnectedUser();
                if (connectedUser == null)
                {
                    return false;
                }

                var channel = await _dbContext.Channels.FirstOrDefaultAsync(x => x.Id == channelId);
                if (channel == null)
                {
                    return false;
                }
                bool isPublic = channel.IsPublic;

                var existingChannelUsers = await _dbContext.ChannelUsers.Where(x => x.ChannelId == channelId).ToListAsync();
                if (!isPublic && existingChannelUsers.Any() && !await IsChannelAdmin(channelId, connectedUser.Id))
                {
                    return false;
                }

                var channelUsersToAdd = new List<ChannelUser>();

                foreach (var username in usernames)
                {
                    var userToAdd = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == username.ToLower());
                    if (userToAdd == null)
                    {
                        return false;
                    }

                    if (await ChannelContainUser(channelId, userToAdd.Id))
                    {
                        continue;
                    }

                    var channelUser = new ChannelUser
                    {
                        Id = Guid.NewGuid(),
                        ChannelId = channelId,
                        UserId = userToAdd.Id,
                        DateCreated = DateTime.UtcNow
                    };

                    // For public channels, assign the first joining user as admin if channel is empty.
                    if (isPublic && !existingChannelUsers.Any() && !channelUsersToAdd.Any())
                    {
                        channelUser.IsAdmin = true;
                    }
                    // For non-public channels, if the sender is being added, assign admin.
                    else if (!isPublic && connectedUser.Id == userToAdd.Id)
                    {
                        channelUser.IsAdmin = true;
                    }

                    channelUsersToAdd.Add(channelUser);
                }

                if (channelUsersToAdd.Any())
                {
                    await _dbContext.ChannelUsers.AddRangeAsync(channelUsersToAdd);
                    await _dbContext.SaveChangesAsync();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemoveChannelUser(Guid channelId, Guid userId)
        {
            var connectedUser = await GetConnectedUser();
            if (connectedUser == null)
            {
                return false;
            }
            if (!await IsChannelAdmin(channelId, connectedUser.Id))
            {
                return false;
            }

            try
            {
                var channelUser = await _dbContext.ChannelUsers.FirstOrDefaultAsync(x => x.ChannelId == channelId && x.UserId == userId);
                if (channelUser == null)
                {
                    return false;
                }
                _dbContext.ChannelUsers.Remove(channelUser);
                await _dbContext.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ChannelContainUser(Guid channelId, Guid userId)
        {
            return await _dbContext.ChannelUsers.FirstOrDefaultAsync(x => x.ChannelId == channelId && x.UserId == userId) != null;
        }

        public async Task<User[]?> GetChannelUsers(Guid channelId)
        {
            HashSet<User> channelUsers = new();
            try
            {
                var currentChannelUsers = await _dbContext.ChannelUsers.Where(x => x.ChannelId == channelId).ToListAsync();
                foreach (var cu in currentChannelUsers)
                {
                    var userdata = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == cu.UserId);
                    if (userdata != null)
                    {
                        channelUsers.Add(userdata);
                    }
                }
            }
            catch { }

            // Only include basic user info
            var users = channelUsers.Select(user => new User
            {
                Id = user.Id,
                Username = user.Username,
                ConnectionId = user.ConnectionId,
                IsOnline = ChatHub.IsUserOnline(user.Id),
            }).ToArray();

            return users;
        }

        public async Task<Channel[]?> GetUserChannels()
        {
            HashSet<Channel> userChannels = new();
            try
            {
                var connectedUser = await GetConnectedUser();
                if (connectedUser == null)
                {
                    return null;
                }

                var channelUsers = await _dbContext.ChannelUsers
                    .Where(x => x.UserId == connectedUser.Id).ToListAsync();
                foreach (var cu in channelUsers)
                {
                    var channel = await _dbContext.Channels.FirstOrDefaultAsync(x => x.Id == cu.ChannelId);
                    if (channel != null)
                    {
                        userChannels.Add(channel);
                    }
                }
            }
            catch { }

            return userChannels.ToArray();
        }

        public async Task<Channel?> GetChannel(Guid channelId)
        {
            var connectedUser = await GetConnectedUser();
            if (connectedUser == null)
            {
                return null;
            }
            var channel = await _dbContext.Channels.FirstOrDefaultAsync(x => x.Id == channelId);
            if (channel == null)
            {
                return null;
            }
            if (!await ChannelContainUser(channelId, connectedUser.Id))
            {
                return null;
            }
            return channel;
        }

        public async Task<bool> IsChannelAdmin(Guid channelId, Guid userId)
        {
            var channelAdmin = await _dbContext.ChannelUsers.FirstOrDefaultAsync(x => x.ChannelId == channelId && x.UserId == userId && x.IsAdmin);
            return channelAdmin != null;
        }

        public async Task<bool> DeleteChannel(Guid channelId)
        {
            var connectedUser = await GetConnectedUser();
            if (connectedUser == null)
            {
                return false;
            }
            if (!await IsChannelAdmin(channelId, connectedUser.Id))
            {
                return false;
            }

            try
            {
                var channel = await _dbContext.Channels.FirstOrDefaultAsync(x => x.Id == channelId);
                if (channel == null)
                {
                    return false;
                }
                _dbContext.Messages.RemoveRange(_dbContext.Messages.Where(x => x.ChannelId == channelId));
                _dbContext.Channels.Remove(channel);
                await _dbContext.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> LeaveChannel(Guid channelId)
        {
            var connectedUser = await GetConnectedUser();
            if (connectedUser == null)
            {
                return false;
            }

            try
            {
                var channelUser = await _dbContext.ChannelUsers.FirstOrDefaultAsync(x => x.ChannelId == channelId && x.UserId == connectedUser.Id);
                if (channelUser == null)
                {
                    return false;
                }
                _dbContext.ChannelUsers.Remove(channelUser);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Channel[]?> SearchPublicChannels(string searchTerm)
        {
            try
            {
                var publicChannels = await _dbContext.Channels.Where(x => x.IsPublic).ToListAsync();
                var result = new List<Channel>();

                foreach (var channel in publicChannels)
                {
                    if (!string.IsNullOrWhiteSpace(channel.Title) &&
                        channel.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(channel);
                    }
                }

                return result.ToArray();
            }
            catch
            {
                return null;
            }
        }
    }
}
