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

            if (string.IsNullOrEmpty(message.Content)
                || string.IsNullOrWhiteSpace(message.Content))
            {
                return false;
            }

            // Encrypt message content
            message.Content = _encryptionService.Encrypt(message.Content);

            //save msg to db
            message.Id = Guid.NewGuid();
            message.DateSent = DateTime.UtcNow;
            message.IsEncrypted = true;

            if (await _messageService.Create(message))
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

            if (!message.IsEncrypted)
                message.Content = _encryptionService.Encrypt(message.Content);

            message.IsEncrypted = true;

            UserPendingMessage userPendingMessage = new()
            {
                UserId = user.Id,
                MessageId = message.Id,
                Content = message.Content,
                IsEncrypted = message.IsEncrypted
            };

            //In case client was offline or had connection cut
            if (!IgnorePendingMessages)
            {
                await _userPendingMessageService.CreateOrUpdate(userPendingMessage);
            }

            string? connectionId = GetUserConnectionIds(user.Id).LastOrDefault();
            if (string.IsNullOrEmpty(connectionId))
            {
                return false;
            }

            try
            {
                Message messageToSend = message;
                if (message.IsEncrypted)
                {
                    messageToSend.Content = _encryptionService.Decrypt(message.Content);
                    messageToSend.IsEncrypted = false;
                }
                await Clients.Client(connectionId).SendAsync("ReceiveMessage", messageToSend);
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

            Message dbMessage = await _messageService.ReadFirst(x => x.ReferenceId == message.ReferenceId);
            if (dbMessage is null)
            {
                return false;
            }
            dbMessage.DateSeen = DateTimeOffset.UtcNow;
            dbMessage.DateUpdated = DateTimeOffset.UtcNow;

            //save msg to db
            if (await _messageService.Update(dbMessage))
            {
                User[]? users = await GetChannelUsers(dbMessage.ChannelId);
                if (users is null)
                {
                    return false;
                }
                foreach (User user in users)
                {
                    string? connectionId = GetUserConnectionIds(user.Id).LastOrDefault();
                    if (string.IsNullOrEmpty(connectionId))
                    {
                        continue;
                    }

                    await Clients.Client(connectionId).SendAsync("UpdateMessage", dbMessage);
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
                await _userPendingMessageService
                .Read(x => x.UserId == ConnectedUser.Id);

            if (UserPendingMessages is not null)
            {
                foreach (var userpendingmessage in UserPendingMessages)
                {
                    Message? message = await _messageService
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
                await _userPendingMessageService
                .ReadFirst(x => x.UserId == ConnectedUser.Id
                    && x.MessageId == messageid
                    && x.DateUserReceivedOn is null
                    && x.DateDeleted is null);

            if (UserPendingMessage is not null)
            {
                await _userPendingMessageService.Delete(x => x.Id == UserPendingMessage.Id);
            }
        }
    }
}