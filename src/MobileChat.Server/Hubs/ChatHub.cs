using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using MobileChat.Server.Helpers;
using MobileChat.Server.Interfaces;
using MobileChat.Shared.Models;

namespace MobileChat.Server.Hubs
{
    public class ChatHub : Hub, IChatHub
    {
        public ChatHub(IUser userService, IMessage messageService, IChannel channelService)
        {
            UserService = userService;
            MessageService = messageService;
            ChannelService = channelService;
        }

        [Inject]
        private IUser UserService { get; set; }
        [Inject]
        private IMessage MessageService { get; set; }
        [Inject]
        private IChannel ChannelService { get; set; }
        public override Task OnConnectedAsync()
        {
            //set user IsOnline true when he connects or reconnects
            User connectedUser = UserService.ReadByConnectionId(Context.ConnectionId).Result;
            if (connectedUser != null)
            {
                connectedUser.IsOnline = true;
                UserService.Update(connectedUser);
            }

            return base.OnConnectedAsync();
        }
        public override Task OnDisconnectedAsync(Exception exception)
        {
            //set user IsOnline false when he disconnects
            User connectedUser = UserService.ReadByConnectionId(Context.ConnectionId).Result;
            if (connectedUser != null)
            {
                connectedUser.IsOnline = false;
                UserService.Update(connectedUser);
            }

            return base.OnDisconnectedAsync(exception);
        }
        public async Task<KeyValuePair<Guid, bool>> SignUp(string displayname, string username, string email, string password)
        {
            if (await UserService.UserExist(username))
            {
                return new KeyValuePair<Guid, bool>(Guid.Empty, false);
            }

            User user = new()
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = email,
                Password = password,
                DisplayName = displayname,
                ConnectionId = Context.ConnectionId,
                DateCreated = DateTime.UtcNow,
                IsOnline = true
            };

            if (await UserService.Create(user))
            {
                return new KeyValuePair<Guid, bool>(user.Id, true);
            }

            return new KeyValuePair<Guid, bool>(Guid.Empty, false);
        }
        public async Task<KeyValuePair<Guid, bool>> SignIn(string emailorusername, string password)
        {
            if (!await UserService.UserExist(emailorusername))
            {
                return new KeyValuePair<Guid, bool>(Guid.Empty, false);
            }

            if (!await UserService.SignIn(emailorusername, password))
            {
                return new KeyValuePair<Guid, bool>(Guid.Empty, false);
            }

            if (PatternMatchHelper.IsEmail(emailorusername))
            {
                User registeredUser = await UserService.ReadByEmail(emailorusername);
                registeredUser.ConnectionId = Context.ConnectionId;
                registeredUser.IsOnline = true;
                await UserService.Update(registeredUser);

                return new KeyValuePair<Guid, bool>(registeredUser.Id, true);
            }
            else
            {
                User registeredUser = await UserService.ReadByUsername(emailorusername);
                registeredUser.ConnectionId = Context.ConnectionId;
                registeredUser.IsOnline = true;
                await UserService.Update(registeredUser);

                return new KeyValuePair<Guid, bool>(registeredUser.Id, true);
            }
        }
        public async Task<bool> ChangePassword(string emailorusername, string newpassword)
        {
            if (await UserService.UserExist(emailorusername))
            {
                if (PatternMatchHelper.IsEmail(emailorusername))
                {
                    User registeredUser = await UserService.ReadByEmail(emailorusername);
                    registeredUser.Password = newpassword;
                    await UserService.Update(registeredUser);

                    return true;
                }
                else
                {
                    User registeredUser = await UserService.ReadByUsername(emailorusername);
                    registeredUser.Password = newpassword;
                    await UserService.Update(registeredUser);

                    return true;
                }
            }

            return false;
        }
        public async Task<string> GetUserDisplayName(Guid userId)
        {
            string displayname = await UserService.GetDisplayName(userId);
            return displayname;
        }

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

            await ChannelService.Create(channel);
            await ChannelService.AddUsers(userId, channel.Id, usernames);

            return channel;
        }
        public async Task<User[]> GetChannelUsers(Guid channelid)
        {
            HashSet<User> channelUsers = await ChannelService.GetUsers(channelid);
            //only send users ids and display names
            List<User> users = new();
            foreach (User user in channelUsers)
            {
                users.Add(new User
                {
                    Id = user.Id,
                    DisplayName = user.DisplayName
                });
            }
            return users.ToArray();
        }
        public async Task<Channel[]> GetUserChannels(Guid userid)
        {
            HashSet<Channel> userChannels = await ChannelService.GetUserChannels(userid);
            return userChannels.ToArray();
        }

        public async Task<bool> SendMessage(Message message)
        {
            if (message == null)
            {
                return false;
            }

            if (message.SenderId == Guid.Empty)
            {
                return false;
            }

            if (string.IsNullOrEmpty(message.Content) || string.IsNullOrWhiteSpace(message.Content))
            {
                return false;
            }

            //save msg to db
            message.Sent = true;
            message.DateSent = DateTime.UtcNow;
            if (await MessageService.Create(message))
            {
                foreach (User user in await ChannelService.GetUsers(message.ChannelId))
                {
                    await Clients.Client(user.ConnectionId).SendAsync("ReceiveMessage", message);
                }

                return true;
            }

            return false;
        }
        public async Task<bool> UpdateMessage(Message message)
        {
            if (message == null)
            {
                return false;
            }

            if (message.SenderId == Guid.Empty)
            {
                return false;
            }

            if (string.IsNullOrEmpty(message.Content) || string.IsNullOrWhiteSpace(message.Content))
            {
                return false;
            }

            //save msg to db
            if (await MessageService.Update(message))
            {
                foreach (User user in await ChannelService.GetUsers(message.ChannelId))
                {
                    await Clients.Client(user.ConnectionId).SendAsync("ReceiveMessage", message);
                }

                return true;
            }

            return false;
        }
        public async Task<Message[]> ReceiveMessageHistory(Guid channelId)
        {
            HashSet<Message> msgs = await ChannelService.GetChannelMessages(channelId);
            return msgs.ToArray();
        }
        public async Task<Message[]> ReceiveMessageHistoryRange(Guid channelId, int index, int range)
        {
            HashSet<Message> msgs = (await ChannelService.GetChannelMessages(channelId)).Skip(index).Take(range).ToHashSet();
            return msgs.ToArray();
        }
        public async Task<bool> AddFriend(Guid userId, string friendEmailorusername)
        {
            if (await UserService.AddFriend(userId, friendEmailorusername))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public async Task<bool> RemoveFriend(Guid userId, string friendEmailorusername)
        {
            if (await UserService.RemoveFriend(userId, friendEmailorusername))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}