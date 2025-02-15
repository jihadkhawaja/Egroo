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
                    await SendClientMessage(user, message, false);
                }

                return true;
            }

            return false;
        }
        private async Task<bool> SendClientMessage(User user, Message message, bool IgnorePendingMessages)
        {
            if (string.IsNullOrWhiteSpace(message.Content))
            {
                return false;
            }

            UserPendingMessage userPendingMessage = new()
            {
                UserId = user.Id,
                MessageId = message.Id,
                Content = message.Content
            };

            //In case client was offline or had connection cut
            if (!IgnorePendingMessages)
            {
                await UserPendingMessageService.CreateOrUpdate(userPendingMessage);
            }

            if (string.IsNullOrEmpty(user.ConnectionId))
            {
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
            }

            return false;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> UpdateMessage(Message message)
        {
            if (message.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(message.Content))
            {
                return false;
            }

            Message dbMessage = await MessageService.ReadFirst(x => x.ReferenceId == message.ReferenceId);
            if (dbMessage is null)
            {
                return false;
            }
            dbMessage.DateSeen = DateTimeOffset.UtcNow;
            dbMessage.DateUpdated = DateTimeOffset.UtcNow;

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
                    Message? message = await MessageService
                        .ReadFirst(x => x.Id == userpendingmessage.MessageId);

                    if (message is null) continue;

                    message.Content = userpendingmessage.Content;

                    await SendClientMessage(ConnectedUser, message, true);
                }
            }
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task UpdatePendingMessage(Guid messageid)
        {
            var ConnectedUser = await GetConnectedUser();

            UserPendingMessage UserPendingMessage =
                await UserPendingMessageService
                .ReadFirst(x => x.UserId == ConnectedUser.Id
                    && x.MessageId == messageid
                    && x.DateUserReceivedOn is null
                    && x.DateDeleted is null);

            if (UserPendingMessage is not null)
            {
                UserPendingMessage.DateDeleted = DateTimeOffset.UtcNow;
                UserPendingMessage.DateUserReceivedOn = DateTimeOffset.UtcNow;
                UserPendingMessage.Content = null;
                await UserPendingMessageService
                .Update(UserPendingMessage);
            }
        }
    }
}