using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

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

            await _channelService.Create(channel);

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

                ChannelUser[] channelUsers = new ChannelUser[usernames.Length];

                for (int i = 0; i < usernames.Length; i++)
                {
                    User? userToAdd = await _userService.ReadFirst(x => x.Username == usernames[i].ToLower());

                    if (userToAdd is null)
                    {
                        return false;
                    }

                    Guid currentuserid = userToAdd.Id;

                    if (await ChannelContainUser(channelid, currentuserid))
                    {
                        continue;
                    }

                    channelUsers[i] = new ChannelUser()
                    {
                        Id = Guid.NewGuid(),
                        ChannelId = channelid,
                        UserId = currentuserid,
                        DateCreated = DateTime.UtcNow,
                    };

                    if (ConnectedUser.Id == currentuserid)
                    {
                        channelUsers[i].IsAdmin = true;
                    }
                }

                await _channelUsersService.Create(channelUsers);

                return true;
            }
            catch { }

            return false;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> ChannelContainUser(Guid channelid, Guid userid)
        {
            return await _channelUsersService.ReadFirst(x => x.ChannelId == channelid && x.UserId == userid) != null;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<User[]?> GetChannelUsers(Guid channelid)
        {
            HashSet<User> channelUsers = new();
            try
            {
                List<ChannelUser> currentChannelUsers = (await _channelUsersService.Read(x => x.ChannelId == channelid)).ToList();
                foreach (ChannelUser user in currentChannelUsers)
                {
                    var userdata = await _userService.ReadFirst(x => x.Id == user.UserId);

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

                List<ChannelUser> channelUsers = (await _channelUsersService
                    .Read(x => x.UserId == ConnectedUser.Id)).ToList();
                foreach (ChannelUser cu in channelUsers)
                {
                    Channel? channel = await _channelService.ReadFirst(x => x.Id == cu.ChannelId);

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
        public async Task<bool> IsChannelAdmin(Guid channelId, Guid userId)
        {
            ChannelUser? channelAdmin = await _channelUsersService.ReadFirst(x => x.ChannelId == channelId && x.UserId == userId && x.IsAdmin);

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

            if (!await _channelUsersService.Delete(x => x.ChannelId == channelId))
            {
                return false;
            }

            if (!await _messageService.Delete(x => x.ChannelId == channelId))
            {
                return false;
            }

            return await _channelService.Delete(x => x.Id == channelId);
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> LeaveChannel(Guid channelId)
        {
            var ConnectedUser = await GetConnectedUser();

            if (ConnectedUser == null)
            {
                return false;
            }

            return await _channelUsersService.Delete(x => x.UserId == ConnectedUser.Id && x.ChannelId == channelId);
        }
    }
}