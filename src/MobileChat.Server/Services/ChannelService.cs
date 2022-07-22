using JihadKhawaja.SignalR.Server.Chat.Models;
using Microsoft.EntityFrameworkCore;
using MobileChat.Server.Database;
using MobileChat.Server.Interfaces;

namespace MobileChat.Server.Services
{
    public class ChannelService : IChannel
    {
        private readonly DataContext context;
        public ChannelService(DataContext context)
        {
            this.context = context;
        }

        public Task<bool> Create(Channel entry)
        {
            try
            {
                context.Channels.Add(entry);
                context.SaveChanges();

                return Task.FromResult(true);
            }
            catch { }
            
            return Task.FromResult(false);
        }

        public Task<Channel> ReadById(Guid id)
        {
            return Task.FromResult(context.Channels.FirstOrDefault(x => x.Id == id));
        }

        public Task<bool> AddUsers(Guid userid, Guid channelid, params string[] usernames)
        {
            try
            {
                ChannelUser[] channelUsers = new ChannelUser[usernames.Length];

                for (int i = 0; i < usernames.Length; i++)
                {
                    Guid currentuserid = context.Users.FirstOrDefault(x => x.Username == usernames[i]).Id;

                    if (ContainUser(currentuserid).Result)
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
                }

                context.ChannelUsers.AddRange(channelUsers);
                context.SaveChanges();

                return Task.FromResult(true);
            }
            catch { }

            return Task.FromResult(false);
        }

        public Task<HashSet<User>> GetUsers(Guid channelid)
        {
            HashSet<User> channelUsers = new();
            try
            {
                List<ChannelUser> users = context.ChannelUsers.Where(x => x.ChannelId == channelid).ToList();
                foreach (ChannelUser user in users)
                {
                    channelUsers.Add(context.Users.FirstOrDefault(x => x.Id == user.UserId));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return Task.FromResult(channelUsers);
            }

            return Task.FromResult(channelUsers);
        }

        public Task<bool> ContainUser(Guid userid)
        {
            return Task.FromResult(context.ChannelUsers.Any(x => x.UserId == userid));
        }

        public Task<HashSet<Channel>> GetUserChannels(Guid userid)
        {
            HashSet<Channel> channels = new();
            try
            {
                List<ChannelUser> users = context.ChannelUsers.Where(x => x.UserId == userid).ToList();
                foreach (ChannelUser user in users)
                {
                    channels.Add(context.Channels.FirstOrDefault(x => x.Id == user.ChannelId));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return Task.FromResult(channels);
            }

            return Task.FromResult(channels);
        }

        public Task<HashSet<Message>> GetChannelMessages(Guid channelid)
        {
            return Task.FromResult(context.Messages.Where(x => x.ChannelId == channelid).ToHashSet());
        }
    }
}
