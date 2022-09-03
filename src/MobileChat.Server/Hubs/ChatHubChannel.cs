using MobileChat.Shared.Interfaces;
using MobileChat.Shared.Models;

namespace MobileChat.Server.Hubs
{
    public partial class ChatHub : IChatChannel
    {
        public async Task<Channel> CreateChannel(Guid userId, params string[] usernames)
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

            await AddChannelUsers(userId, channel.Id, usernames);

            return channel;
        }
        public async Task<bool> AddChannelUsers(Guid userid, Guid channelid, params string[] usernames)
        {
            try
            {
                ChannelUser[] channelUsers = new ChannelUser[usernames.Length];

                for (int i = 0; i < usernames.Length; i++)
                {
                    User currentuser = (await UserService.Read(x => x.Username == usernames[i])).FirstOrDefault();
                    if (currentuser is null)
                    {
                        return false;
                    }

                    Guid currentuserid = currentuser.Id;

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

                    if (userid == currentuserid)
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
        public async Task<bool> ChannelContainUser(Guid channelid, Guid userid)
        {
            return (await ChannelUsersService.Read(x => x.ChannelId == channelid && x.UserId == userid)).FirstOrDefault() != null;
        }
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
        public async Task<Channel[]> GetUserChannels(Guid userid)
        {
            HashSet<Channel> userChannels = new();
            try
            {
                List<ChannelUser> users = (await ChannelUsersService.Read(x => x.UserId == userid)).ToList();
                foreach (ChannelUser user in users)
                {
                    userChannels.Add((await ChannelService.Read(x => x.Id == user.ChannelId)).FirstOrDefault());
                }
            }
            catch { }

            return userChannels.ToArray();
        }

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

        public async Task<bool> DeleteChannel(Guid channelId, Guid userId)
        {
            if (!await IsChannelAdmin(channelId, userId))
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

        public async Task<bool> LeaveChannel(Guid userId, Guid channelId)
        {
            return await ChannelUsersService.Delete(x => x.UserId == userId && x.ChannelId == channelId);
        }
    }
}