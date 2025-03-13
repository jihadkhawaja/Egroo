using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : IChatChannel
    {
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<Channel?> CreateChannel(params string[] usernames)
        {
            if (usernames.Length == 0)
            {
                return null;
            }

            Channel channel = new()
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

            await AddChannelUsers(channel.Id, usernames);

            return channel;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> AddChannelUsers(Guid channelid, params string[] usernames)
        {
            try
            {
                var ConnectedUser = await GetConnectedUser();

                if (ConnectedUser == null)
                {
                    return false;
                }

                // Get the channel to check if it is public.
                Channel? channel = await _dbContext.Channels.FirstOrDefaultAsync(x => x.Id == channelid);
                if (channel == null)
                {
                    return false;
                }
                bool isPublic = channel.IsPublic;

                // Retrieve existing channel users.
                var existingChannelUsers = await _dbContext.ChannelUsers.Where(x => x.ChannelId == channelid).ToListAsync();
                // For non-public channels, only admins may add users once there are existing users.
                if (!isPublic && existingChannelUsers.Any() && !await IsChannelAdmin(channelid, ConnectedUser.Id))
                {
                    return false;
                }

                ChannelUser[] channelUsersToBeAdded = new ChannelUser[usernames.Length];
                int newUsersAddedCount = 0;

                for (int i = 0; i < usernames.Length; i++)
                {
                    User? userToAdd = await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == usernames[i].ToLower());

                    if (userToAdd is null)
                    {
                        return false;
                    }

                    Guid currentuserid = userToAdd.Id;

                    if (await ChannelContainUser(channelid, currentuserid))
                    {
                        continue;
                    }

                    var channelUser = new ChannelUser()
                    {
                        Id = Guid.NewGuid(),
                        ChannelId = channelid,
                        UserId = currentuserid,
                        DateCreated = DateTime.UtcNow,
                    };

                    // For public channels, assign the first joining user as admin (creator) if the channel is empty.
                    if (isPublic &&
                        !existingChannelUsers.Any() && // no users in channel already
                        newUsersAddedCount == 0)
                    {
                        channelUser.IsAdmin = true;
                    }
                    // For non-public channels, if the sender is the one being added, assign admin.
                    else if (!isPublic && ConnectedUser.Id == currentuserid)
                    {
                        channelUser.IsAdmin = true;
                    }

                    channelUsersToBeAdded[i] = channelUser;
                    newUsersAddedCount++;
                }

                await _dbContext.ChannelUsers.AddRangeAsync(channelUsersToBeAdded);
                await _dbContext.SaveChangesAsync();

                await NotifyChannelChange(channelid);

                return true;
            }
            catch
            {
                return false;
            }
        }
        //remove user by channel admin
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> RemoveChannelUser(Guid channelid, Guid userid)
        {
            var ConnectedUser = await GetConnectedUser();
            if (ConnectedUser == null)
            {
                return false;
            }
            if (!await IsChannelAdmin(channelid, ConnectedUser.Id))
            {
                return false;
            }

            try
            {
                ChannelUser? channelUser = await _dbContext.ChannelUsers.FirstOrDefaultAsync(x => x.ChannelId == channelid && x.UserId == userid);
                if (channelUser == null)
                {
                    return false;
                }
                _dbContext.ChannelUsers.Remove(channelUser);
                await _dbContext.SaveChangesAsync();

                await NotifyChannelChange(channelid, userid);

                return true;
            }
            catch
            {
                return false;
            }
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> ChannelContainUser(Guid channelid, Guid userid)
        {
            return await _dbContext.ChannelUsers.FirstOrDefaultAsync(x => x.ChannelId == channelid && x.UserId == userid) != null;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<User[]?> GetChannelUsers(Guid channelid)
        {
            HashSet<User> channelUsers = new();
            try
            {
                List<ChannelUser> currentChannelUsers = await _dbContext.ChannelUsers.Where(x => x.ChannelId == channelid).ToListAsync();
                foreach (ChannelUser user in currentChannelUsers)
                {
                    var userdata = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == user.UserId);

                    if (userdata != null)
                    {
                        channelUsers.Add(userdata);
                    }
                }
            }
            catch { }

            //only send users ids and display names
            List<User> users = new();
            foreach (User user in channelUsers)
            {
                users.Add(new User
                {
                    Id = user.Id,
                    Username = user.Username,
                    ConnectionId = GetUserConnectionIds(user.Id).LastOrDefault(),
                    IsOnline = IsUserOnline(user.Id),
                });
            }

            return users.ToArray();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<Channel[]?> GetUserChannels()
        {
            HashSet<Channel> userChannels = new();
            try
            {
                var ConnectedUser = await GetConnectedUser();

                List<ChannelUser> channelUsers = await _dbContext.ChannelUsers
                    .Where(x => x.UserId == ConnectedUser.Id).ToListAsync();
                foreach (ChannelUser cu in channelUsers)
                {
                    Channel? channel = await _dbContext.Channels.FirstOrDefaultAsync(x => x.Id == cu.ChannelId);

                    if (channel == null)
                    {
                        continue;
                    }

                    userChannels.Add(channel);
                }
            }
            catch { }

            return userChannels.ToArray();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<Channel?> GetChannel(Guid channelId)
        {
            var ConnectedUser = await GetConnectedUser();
            if (ConnectedUser == null)
            {
                return null;
            }
            Channel? channel = await _dbContext.Channels.FirstOrDefaultAsync(x => x.Id == channelId);
            if (channel == null)
            {
                return null;
            }
            if (!await ChannelContainUser(channelId, ConnectedUser.Id))
            {
                return null;
            }
            return channel;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> IsChannelAdmin(Guid channelId, Guid userId)
        {
            ChannelUser? channelAdmin = await _dbContext.ChannelUsers.FirstOrDefaultAsync(x => x.ChannelId == channelId && x.UserId == userId && x.IsAdmin);

            if (channelAdmin is null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> DeleteChannel(Guid channelId)
        {
            var ConnectedUser = await GetConnectedUser();

            if (!await IsChannelAdmin(channelId, ConnectedUser.Id))
            {
                return false;
            }

            // Capture channel users before deletion.
            User[]? prevChannelUsers = await GetChannelUsers(channelId);
            Guid[] prevUserIds = prevChannelUsers?.Select(u => u.Id).ToArray() ?? Array.Empty<Guid>();

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

                await NotifyChannelChange(channelId, prevUserIds);

                return true;
            }
            catch
            {
                return false;
            }
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> LeaveChannel(Guid channelId)
        {
            var ConnectedUser = await GetConnectedUser();

            if (ConnectedUser == null)
            {
                return false;
            }

            try
            {
                ChannelUser? channelUser = await _dbContext.ChannelUsers.FirstOrDefaultAsync(x => x.ChannelId == channelId && x.UserId == ConnectedUser.Id);
                if (channelUser == null)
                {
                    return false;
                }
                _dbContext.ChannelUsers.Remove(channelUser);
                await _dbContext.SaveChangesAsync();
                await NotifyChannelChange(channelId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<Channel[]?> SearchPublicChannels(string searchTerm)
        {
            try
            {
                var publicChannels = await _dbContext.Channels.Where(x => x.IsPublic).ToListAsync();
                List<Channel> result = new();

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

        //notify about channel changes
        private async Task NotifyChannelChange(Guid channelId, params Guid[]? extraUserIds)
        {
            var channelUsers = await GetChannelUsers(channelId);
            HashSet<Guid> notifiedUserIds = new();

            // Notify all users currently in the channel
            foreach (User user in channelUsers)
            {
                notifiedUserIds.Add(user.Id);
                var userConns = GetUserConnectionIds(user.Id);
                await Clients.Clients(userConns).SendAsync("ChannelChange", channelId);
            }

            // Additionally notify users provided in the extraUserIds parameter if not already notified
            if (extraUserIds != null)
            {
                foreach (Guid userId in extraUserIds)
                {
                    if (!notifiedUserIds.Contains(userId))
                    {
                        var userConns = GetUserConnectionIds(userId);
                        await Clients.Clients(userConns).SendAsync("ChannelChange", channelId);
                    }
                }
            }
        }
    }
}