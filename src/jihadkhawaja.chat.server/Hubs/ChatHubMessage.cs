using jihadkhawaja.chat.server.Repository;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : IMessageHub
    {
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> SendMessage(Message message)
        {
            if (message == null)
                return false;

            var connectedUser = await GetConnectedUser();
            if (connectedUser == null || connectedUser.Id != message.SenderId)
                return false;

            if (!await ChannelContainUser(message.ChannelId, message.SenderId))
                return false;

            if (string.IsNullOrWhiteSpace(message.Content))
                return false;

            bool dbResult = await _messageRepository.SendMessage(message);
            if (!dbResult)
                return false;

            User[]? users = await GetChannelUsers(message.ChannelId);
            if (users == null)
                return false;

            foreach (User user in users)
            {
                await SendClientMessage(user, message, ignorePendingMessages: false);
            }

            return true;
        }

        private async Task<bool> SendClientMessage(User user, Message message, bool ignorePendingMessages)
        {
            if (string.IsNullOrWhiteSpace(message.Content))
                return false;

            // Make sure the message content is encrypted.
            if (!message.IsEncrypted)
            {
                message.Content = _encryptionService.Encrypt(message.Content);
            }
            message.IsEncrypted = true;

            UserPendingMessage pendingMsg = new()
            {
                UserId = user.Id,
                MessageId = message.Id,
                Content = message.Content,
                IsEncrypted = message.IsEncrypted
            };

            if (_messageRepository is MessageRepository concreteRepo)
            {
                // Save pending message if the client is offline.
                if (!ignorePendingMessages)
                {
                    bool pendingSaved = await concreteRepo.AddPendingMessage(pendingMsg);
                    if (!pendingSaved)
                        return false;
                }
            }
            else
            {
                // Cannot save pending message without the concrete implementation.
                return false;
            }

            string? connectionId = GetUserConnectionIds(user.Id).LastOrDefault();
            if (string.IsNullOrEmpty(connectionId))
                return false;

            try
            {
                // Decrypt before sending to client.
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
                Console.WriteLine($"Failed to send message: {ex}");
            }

            return false;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> UpdateMessage(Message message)
        {
            if (message.Id == Guid.Empty || string.IsNullOrWhiteSpace(message.Content))
                return false;

            bool dbResult = await _messageRepository.UpdateMessage(message);
            if (!dbResult)
                return false;

            Message? updatedMessage = null;
            if (_messageRepository is MessageRepository concreteRepo)
            {
                updatedMessage = await concreteRepo.GetMessageByReferenceId(message.ReferenceId);
            }
            if (updatedMessage == null)
                return false;

            User[]? users = await GetChannelUsers(updatedMessage.ChannelId);
            if (users == null)
                return false;

            foreach (User user in users)
            {
                string? connectionId = GetUserConnectionIds(user.Id).LastOrDefault();
                if (string.IsNullOrEmpty(connectionId))
                    continue;
                await Clients.Client(connectionId).SendAsync("UpdateMessage", updatedMessage);
            }

            return true;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task SendPendingMessages()
        {
            var connectedUser = await GetConnectedUser();
            if (connectedUser == null)
                return;

            if (_messageRepository is not MessageRepository concreteRepo)
                return;

            IEnumerable<UserPendingMessage> pendingMessages = await concreteRepo.GetPendingMessagesForUser(connectedUser.Id);
            foreach (var pending in pendingMessages)
            {
                var message = await concreteRepo.GetMessageById(pending.MessageId);
                if (message == null)
                    continue;
                message.Content = pending.Content;
                await SendClientMessage(connectedUser, message, ignorePendingMessages: true);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task UpdatePendingMessage(Guid messageid)
        {
            var connectedUser = await GetConnectedUser();
            if (connectedUser == null)
                return;

            await _messageRepository.UpdatePendingMessage(messageid);
        }
    }
}
