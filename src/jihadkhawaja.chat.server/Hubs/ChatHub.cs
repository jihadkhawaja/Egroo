using jihadkhawaja.chat.server.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace jihadkhawaja.chat.server.Hubs
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
        [Inject]
        private IEntity<UserPendingMessage> UserPendingMessageService { get; set; }
        public ChatHub(IEntity<User> UserService, IEntity<UserFriend> UserFriendsService,
            IEntity<Channel> ChannelService, IEntity<ChannelUser> ChannelUsersService,
            IEntity<Message> MessageService, IEntity<UserPendingMessage> UserPendingMessageService)
        {
            this.UserService = UserService;
            this.UserFriendsService = UserFriendsService;
            this.ChannelService = ChannelService;
            this.ChannelUsersService = ChannelUsersService;
            this.MessageService = MessageService;
            this.UserPendingMessageService = UserPendingMessageService;
        }

        public async Task<User?> GetConnectedUser()
        {
            HttpContext? hc = Context.GetHttpContext();

            if (hc != null)
            {
                string Token = hc.Request.Query["access_token"];

                //set user IsOnline true when he connects or reconnects
                if (!string.IsNullOrWhiteSpace(Token))
                {
                    //get user id from token
                    JwtSecurityTokenHandler tokenHandler = new();
                    JwtSecurityToken jwtToken = tokenHandler.ReadJwtToken(Token);
                    var userIdClaim = jwtToken.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

                    Guid ConnectorUserId = Guid.Parse(userIdClaim.Value);

                    return await UserService.ReadFirst(x => x.Id == ConnectorUserId);
                }
            }

            return null;
        }
        public override async Task OnConnectedAsync()
        {
            var ConnectedUser = await GetConnectedUser();

            if (ConnectedUser != null)
            {
                // Hang up any calls the user is in
                await HangUp();

                // Gets the user from "Context" which is available in the whole hub
                // Remove the user
                _Users.RemoveAll(u => u.ConnectionId == Context.ConnectionId);

                // Send down the new user list to all clients
                await SendUserListUpdate();

                ConnectedUser.ConnectionId = Context.ConnectionId;
                ConnectedUser.IsOnline = true;

                await UserService.Update(ConnectedUser);
            }

            await base.OnConnectedAsync();
        }
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            User? connectedUser = await UserService
                .ReadFirst(x => x.ConnectionId == Context.ConnectionId);
            if (connectedUser != null)
            {
                connectedUser.ConnectionId = null;
                connectedUser.IsOnline = false;

                await UserService.Update(connectedUser);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}