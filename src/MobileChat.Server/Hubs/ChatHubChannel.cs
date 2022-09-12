using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MobileChat.Shared.Interfaces;
using MobileChat.Shared.Models;

namespace MobileChat.Server.Hubs
{
    public partial class ChatHub : IChatChannel
    {
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<Channel> CreateChannel(params string[] usernames)
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

            Channel[] channels = new Channel[1] { channel };
            await ChannelService.Create(channels);

            await AddChannelUsers(channel.Id, usernames);

            return channel;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> AddChannelUsers(Guid channelid, params string[] usernames)
        {
            try
            {
                string Token = Context.GetHttpContext().Request.Query["access_token"];
                Guid ConnectorUserId = (await UserService.Read(x => x.Token == Token)).FirstOrDefault().Id;

                ChannelUser[] channelUsers = new ChannelUser[usernames.Length];

                for (int i = 0; i < usernames.Length; i++)
                {
                    User userToAdd = (await UserService.Read(x => x.Username == usernames[i])).FirstOrDefault();
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

                    if (ConnectorUserId == currentuserid)
                    {
                        channelUsers[i].IsAdmin = true;
                    }
                }

                await ChannelUsersService.Create(channelUsers);

                return true;
            }
            catch { }

            return false;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> ChannelContainUser(Guid channelid, Guid userid)
        {
            return (await ChannelUsersService.Read(x => x.ChannelId == channelid && x.UserId == userid)).FirstOrDefault() != null;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<User[]> GetChannelUsers(Guid channelid)
        {
            HashSet<User> channelUsers = new();
            try
            {
                List<ChannelUser> currentChannelUsers = (await ChannelUsersService.Read(x => x.ChannelId == channelid)).ToList();
                foreach (ChannelUser user in currentChannelUsers)
                {
                    channelUsers.Add((await UserService.Read(x => x.Id == user.UserId)).FirstOrDefault());
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
                    DisplayName = user.DisplayName,
                    Username = user.Username,
                    ConnectionId = user.ConnectionId,
                });
            }

            return users.ToArray();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<Channel[]> GetUserChannels()
        {
            HashSet<Channel> userChannels = new();
            try
            {
                string Token = Context.GetHttpContext().Request.Query["access_token"];
                Guid ConnectorUserId = (await UserService.Read(x => x.Token == Token)).FirstOrDefault().Id;

                List<ChannelUser> users = (await ChannelUsersService.Read(x => x.UserId == ConnectorUserId)).ToList();
                foreach (ChannelUser user in users)
                {
                    userChannels.Add((await ChannelService.Read(x => x.Id == user.ChannelId)).FirstOrDefault());
                }
            }
            catch { }

            return userChannels.ToArray();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> IsChannelAdmin(Guid channelId, Guid userId)
        {
            ChannelUser channelAdmin = (await ChannelUsersService.Read(x => x.ChannelId == channelId && x.UserId == userId && x.IsAdmin)).FirstOrDefault();

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
            string Token = Context.GetHttpContext().Request.Query["access_token"];
            Guid ConnectorUserId = (await UserService.Read(x => x.Token == Token)).FirstOrDefault().Id;

            if (!await IsChannelAdmin(channelId, ConnectorUserId))
            {
                return false;
            }

            if (!await ChannelUsersService.Delete(x => x.ChannelId == channelId))
            {
                return false;
            }

            if (!await MessageService.Delete(x => x.ChannelId == channelId))
            {
                return false;
            }

            return await ChannelService.Delete(x => x.Id == channelId);
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> LeaveChannel(Guid channelId)
        {
            string Token = Context.GetHttpContext().Request.Query["access_token"];
            Guid ConnectorUserId = (await UserService.Read(x => x.Token == Token)).FirstOrDefault().Id;

            return await ChannelUsersService.Delete(x => x.UserId == ConnectorUserId && x.ChannelId == channelId);
        }
    }
}