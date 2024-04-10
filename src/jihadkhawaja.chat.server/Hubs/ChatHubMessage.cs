using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : IChatMessage
    {
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
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
            message.Id = Guid.NewGuid();
            message.DateCreated = DateTime.UtcNow;
            message.DateSent = DateTime.UtcNow;

            if (await MessageService.Create(message))
            {
                User[]? users = await GetChannelUsers(message.ChannelId);
                if (users is null)
                {
                    return false;
                }
                foreach (User user in users)
                {
                    await SendClientMessage(user, message);
                }

                return true;
            }

            return false;
        }
        private async Task<bool> SendClientMessage(User user, Message message)
        {
            UserPendingMessage userPendingMessage = new()
            {
                UserId = user.Id,
                MessageId = message.Id,
                Content = message.Content
            };

            if (string.IsNullOrEmpty(user.ConnectionId))
            {
                await UserPendingMessageService.Create(userPendingMessage);
                return false;
            }

            try
            {
                await Clients.Client(user.ConnectionId).SendAsync("ReceiveMessage", message);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send message for the following error: {ex}");

                await UserPendingMessageService.Create(userPendingMessage);
            }

            return false;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> SetMessageAsSeen(Guid messageid)
        {
            if (messageid == Guid.Empty)
            {
                return false;
            }

            Message dbMessage = await MessageService.ReadFirst(x => x.Id == messageid);
            dbMessage.DateSeen = DateTime.UtcNow;

            //save msg to db
            if (await MessageService.Update(dbMessage))
            {
                User[]? users = await GetChannelUsers(dbMessage.ChannelId);
                if (users is null)
                {
                    return false;
                }
                foreach (User user in users)
                {
                    if (string.IsNullOrEmpty(user.ConnectionId))
                    {
                        continue;
                    }

                    await Clients.Client(user.ConnectionId).SendAsync("UpdateMessage", dbMessage);
                }

                return true;
            }

            return false;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task SendPendingMessages()
        {
            var ConnectedUser = await GetConnectedUser();

            IEnumerable<UserPendingMessage> UserPendingMessages =
                await UserPendingMessageService
                .Read(x => x.UserId == ConnectedUser.Id);

            if (UserPendingMessages is not null)
            {
                foreach (var userpendingmessage in UserPendingMessages)
                {
                    Message message = await MessageService
                        .ReadFirst(x => x.Id == userpendingmessage.MessageId);
                    message.Content = userpendingmessage.Content;

                    if (await SendClientMessage(ConnectedUser, message))
                    {
                        await UserPendingMessageService
                            .Delete(x => x.Id == userpendingmessage.Id);
                    }
                }
            }
        }
    }
}