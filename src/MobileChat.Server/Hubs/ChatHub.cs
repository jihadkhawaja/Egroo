using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using MobileChat.Server.Interfaces;
using MobileChat.Shared.Models;

namespace MobileChat.Server.Hubs
{
    public partial class ChatHub : Hub
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

                User[] connectedUsers = new User[1] { connectedUser };
                await UserService.Update(connectedUsers);
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

                User[] connectedUsers = new User[1] { connectedUser };
                await UserService.Update(connectedUsers);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}