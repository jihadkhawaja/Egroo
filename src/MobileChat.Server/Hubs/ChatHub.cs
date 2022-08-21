using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using MobileChat.Server.Helpers;
using MobileChat.Server.Interfaces;
using MobileChat.Shared.Interfaces;
using MobileChat.Shared.Models;
using Channel = MobileChat.Shared.Models.Channel;
using Message = MobileChat.Shared.Models.Message;

namespace MobileChat.Server.Hubs
{
    public class ChatHub : Hub, IChatHub
    {
        [Inject]
        private IEntity<User> UserService { get; set; }
        [Inject]
        private IEntity<UserFriend> UserFriendsService { get; set; }
        [Inject]
        private IEntity<Channel> ChannelService { get; set; }
        [Inject]
        private IEntity<ChannelUser> ChannelUsersService { get; set; }
        [Inject]
        private IEntity<Message> MessageService { get; set; }
        public ChatHub(IEntity<User> UserService, IEntity<UserFriend> UserFriendsService,
            IEntity<Channel> ChannelService, IEntity<ChannelUser> ChannelUsersService,
            IEntity<Message> MessageService)
        {
            this.UserService = UserService;
            this.UserFriendsService = UserFriendsService;
            this.ChannelService = ChannelService;
            this.ChannelUsersService = ChannelUsersService;
            this.MessageService = MessageService;
        }

        public override async Task OnConnectedAsync()
        {
            //set user IsOnline true when he connects or reconnects
            User connectedUser = (await UserService.Read(x => x.ConnectionId == Context.ConnectionId)).FirstOrDefault();
            if (connectedUser != null)
            {
                connectedUser.IsOnline = true;
                await UserService.Update(connectedUser);
            }

            await base.OnConnectedAsync();
        }
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            //set user IsOnline false when he disconnects
            User connectedUser = (await UserService.Read(x => x.ConnectionId == Context.ConnectionId)).FirstOrDefault();
            if (connectedUser != null)
            {
                connectedUser.IsOnline = false;
                await UserService.Update(connectedUser);
            }

            await base.OnDisconnectedAsync(exception);
        }
        public async Task<KeyValuePair<Guid, bool>> SignUp(string displayname, string username, string email, string password)
        {
            if ((await UserService.Read(x => x.Username == username)).FirstOrDefault() != null)
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

            User[] users = new User[1] { user };
            if (await UserService.Create(users))
            {
                return new KeyValuePair<Guid, bool>(user.Id, true);
            }

            return new KeyValuePair<Guid, bool>(Guid.Empty, false);
        }
        public async Task<KeyValuePair<Guid, bool>> SignIn(string emailorusername, string password)
        {
            if (PatternMatchHelper.IsEmail(emailorusername))
            {
                if ((await UserService.Read(x => x.Email == emailorusername)).FirstOrDefault() == null)
                {
                    return new KeyValuePair<Guid, bool>(Guid.Empty, false);
                }

                if ((await UserService.Read(x => x.Email == emailorusername && x.Password == password)).FirstOrDefault() == null)
                {
                    return new KeyValuePair<Guid, bool>(Guid.Empty, false);
                }

                User registeredUser = (await UserService.Read(x => x.Email == emailorusername)).FirstOrDefault();
                registeredUser.ConnectionId = Context.ConnectionId;
                registeredUser.IsOnline = true;
                await UserService.Update(registeredUser);

                return new KeyValuePair<Guid, bool>(registeredUser.Id, true);
            }
            else
            {
                if ((await UserService.Read(x => x.Username == emailorusername)).FirstOrDefault() == null)
                {
                    return new KeyValuePair<Guid, bool>(Guid.Empty, false);
                }

                if ((await UserService.Read(x => x.Username == emailorusername && x.Password == password)).FirstOrDefault() == null)
                {
                    return new KeyValuePair<Guid, bool>(Guid.Empty, false);
                }

                User registeredUser = (await UserService.Read(x => x.Username == emailorusername)).FirstOrDefault();
                registeredUser.ConnectionId = Context.ConnectionId;
                registeredUser.IsOnline = true;
                await UserService.Update(registeredUser);

                return new KeyValuePair<Guid, bool>(registeredUser.Id, true);
            }
        }
        public async Task<bool> ChangePassword(string emailorusername, string oldpassword, string newpassword)
        {
            if (PatternMatchHelper.IsEmail(emailorusername))
            {
                User registeredUser = (await UserService.Read(x => x.Email == emailorusername && x.Password == oldpassword)).FirstOrDefault();

                if(registeredUser is null)
                {
                    return false;
                }

                registeredUser.Password = newpassword;
                await UserService.Update(registeredUser);

                return true;
            }
            else
            {
                User registeredUser = (await UserService.Read(x => x.Username == emailorusername && x.Password == oldpassword)).FirstOrDefault();

                if (registeredUser is null)
                {
                    return false;
                }

                registeredUser.Password = newpassword;
                await UserService.Update(registeredUser);

                return true;
            }
        }
        public async Task<string> GetUserDisplayName(Guid userId)
        {
            string displayname = (await UserService.Read(x => x.Id == userId)).FirstOrDefault().DisplayName;

            return displayname;
        }

        public async Task<string> GetUserUsername(Guid userId)
        {
            string username = (await UserService.Read(x => x.Id == userId)).FirstOrDefault().Username;

            return username;
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

            Message[] messages = new Message[1] { message };
            if (await MessageService.Create(messages))
            {
                foreach (User user in await GetChannelUsers(message.ChannelId))
                {
                    try
                    {
                        await Clients.Client(user.ConnectionId).SendAsync("ReceiveMessage", message);
                    }
                    catch { }
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
                foreach (User user in await GetChannelUsers(message.ChannelId))
                {
                    await Clients.Client(user.ConnectionId).SendAsync("ReceiveMessage", message);
                }

                return true;
            }

            return false;
        }
        public async Task<Message[]> ReceiveMessageHistory(Guid channelId)
        {
            HashSet<Message> msgs = (await MessageService.Read(x => x.ChannelId == channelId)).ToHashSet();
            return msgs.ToArray();
        }
        public async Task<Message[]> ReceiveMessageHistoryRange(Guid channelId, int index, int range)
        {
            HashSet<Message> msgs = (await ReceiveMessageHistory(channelId)).Skip(index).Take(range).ToHashSet();
            return msgs.ToArray();
        }
        public async Task<bool> AddFriend(Guid userId, string friendEmailorusername)
        {
            if (string.IsNullOrEmpty(friendEmailorusername))
            {
                return false;
            }

            try
            {
                if (PatternMatchHelper.IsEmail(friendEmailorusername))
                {
                    //get user id from email
                    User user = (await UserService.Read(x => x.Id == userId)).FirstOrDefault();
                    if (user == null)
                    {
                        return false;
                    }
                    //get friend id from email
                    User friendUser = (await UserService.Read(x => x.Email == friendEmailorusername)).FirstOrDefault();
                    if (friendUser == null)
                    {
                        return false;
                    }

                    if ((await UserFriendsService.Read(x => (x.UserId == user.Id && x.FriendUserId == friendUser.Id)
                    || (x.FriendUserId == user.Id && x.UserId == friendUser.Id))).FirstOrDefault() != null)
                    {
                        return false;
                    }

                    UserFriend entry = new() { UserId = user.Id, FriendUserId = friendUser.Id, DateCreated = DateTime.UtcNow };
                    UserFriend[] entries = new UserFriend[1] { entry };
                    await UserFriendsService.Create(entries);
                }
                else
                {
                    //get user id from username
                    User user = (await UserService.Read(x => x.Id == userId)).FirstOrDefault();
                    if (user == null)
                    {
                        return false;
                    }
                    //get friend id from username
                    User friendUser = (await UserService.Read(x => x.Username == friendEmailorusername)).FirstOrDefault();
                    if (friendUser == null)
                    {
                        return false;
                    }

                    if ((await UserFriendsService.Read(x => (x.UserId == user.Id && x.FriendUserId == friendUser.Id)
                    || (x.FriendUserId == user.Id && x.UserId == friendUser.Id))).FirstOrDefault() != null)
                    {
                        return false;
                    }

                    UserFriend entry = new() { Id = Guid.NewGuid(), UserId = user.Id, FriendUserId = friendUser.Id, DateCreated = DateTime.UtcNow };
                    UserFriend[] entries = new UserFriend[1] { entry };
                    await UserFriendsService.Create(entries);
                }
            }
            catch
            {
                return false;
            }

            return true;
        }
        public async Task<bool> RemoveFriend(Guid userId, string friendEmailorusername)
        {
            if (string.IsNullOrEmpty(friendEmailorusername))
            {
                return false;
            }

            try
            {
                if (PatternMatchHelper.IsEmail(friendEmailorusername))
                {
                    //get user id from email
                    User user = (await UserService.Read(x => x.Id == userId)).FirstOrDefault();
                    if (user == null)
                    {
                        return false;
                    }
                    //get friend id from email
                    User friendUser = (await UserService.Read(x => x.Email == friendEmailorusername)).FirstOrDefault();
                    if (friendUser == null)
                    {
                        return false;
                    }

                    if ((await UserFriendsService.Read(x => (x.UserId == user.Id && x.FriendUserId == friendUser.Id)
                    || (x.FriendUserId == user.Id && x.UserId == friendUser.Id))).FirstOrDefault() != null)
                    {
                        return false;
                    }

                    UserFriend entry = new() { UserId = user.Id, FriendUserId = friendUser.Id, DateCreated = DateTime.UtcNow };

                    await UserFriendsService.Delete(x => x.Id == entry.Id);
                }
                else
                {
                    //get user id from username
                    User user = (await UserService.Read(x => x.Id == userId)).FirstOrDefault();
                    if (user == null)
                    {
                        return false;
                    }
                    //get friend id from username
                    User friendUser = (await UserService.Read(x => x.Username == friendEmailorusername)).FirstOrDefault();
                    if (friendUser == null)
                    {
                        return false;
                    }

                    if ((await UserFriendsService.Read(x => (x.UserId == user.Id && x.FriendUserId == friendUser.Id)
                    || (x.FriendUserId == user.Id && x.UserId == friendUser.Id))).FirstOrDefault() == null)
                    {
                        return false;
                    }

                    UserFriend entry = new() { UserId = user.Id, FriendUserId = friendUser.Id, DateCreated = DateTime.UtcNow };

                    await UserFriendsService.Delete(x => (x.UserId == user.Id && x.FriendUserId == friendUser.Id)
                    || (x.FriendUserId == user.Id && x.UserId == friendUser.Id));
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        public async Task<UserFriend[]> GetUserFriends(Guid userId)
        {
            return (await UserFriendsService.Read(x => (x.UserId == userId && x.IsAccepted) || (x.FriendUserId == userId && x.IsAccepted))).ToArray();
        }

        public async Task<UserFriend[]> GetUserFriendRequests(Guid userId)
        {
            return (await UserFriendsService.Read(x => x.FriendUserId == userId && !x.IsAccepted)).ToArray();
        }

        public async Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
        {
            UserFriend result = (await UserFriendsService.Read(x => x.UserId == userId && x.FriendUserId == friendId && x.IsAccepted)).FirstOrDefault();

            if (result is null)
            {
                return false;
            }

            return result.IsAccepted;
        }

        public async Task<bool> AcceptFriend(Guid userId, Guid friendId)
        {
            UserFriend friendRequest = (await UserFriendsService.Read(x => x.UserId == friendId && x.FriendUserId == userId && !x.IsAccepted)).FirstOrDefault();

            if (friendRequest is null)
            {
                return false;
            }

            friendRequest.IsAccepted = true;

            return await UserFriendsService.Update(friendRequest);
        }

        public async Task<bool> DenyFriend(Guid userId, Guid friendId)
        {
            return await UserFriendsService.Delete(x => x.UserId == friendId && x.FriendUserId == userId && !x.IsAccepted);
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
    }
}