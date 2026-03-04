using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : IMessageHub
    {
        [Authorize]
        public async Task<bool> SendMessage(Message message)
        {
            if (message == null)
                return false;

            var userId = GetUserIdFromContext();
            if (!userId.HasValue || userId.Value != message.SenderId)
                return false;

            if (!await ChannelContainUser(message.ChannelId, message.SenderId))
                return false;

            if (string.IsNullOrWhiteSpace(message.Content))
                return false;

            bool dbResult = await _messageRepository.SendMessage(message);
            if (!dbResult)
                return false;

            UserDto[]? users = await GetChannelUsers(message.ChannelId);
            if (users == null)
                return false;

            foreach (UserDto user in users)
            {
                await SendClientMessage(user, message, ignorePendingMessages: false);
            }

            // Trigger agent mention processing (fire-and-forget)
            if (_agentChannelResponder is not null)
            {
                _ = Task.Run(() => _agentChannelResponder.ProcessMentionsAsync(message));
            }

            return true;
        }

        private async Task<bool> SendClientMessage(UserDto user, Message message, bool ignorePendingMessages)
        {
            if (string.IsNullOrWhiteSpace(message.Content))
                return false;

            UserPendingMessage pendingMsg = new()
            {
                UserId = user.Id,
                MessageId = message.Id,
                Content = message.Content,
            };

            if (!ignorePendingMessages)
            {
                bool pendingSaved = await _messageRepository.AddPendingMessage(pendingMsg);
                if (!pendingSaved)
                    return false;
            }

            string? connectionId = _connectionTracker.GetUserConnectionIds(user.Id).LastOrDefault();
            if (string.IsNullOrEmpty(connectionId))
                return false;

            try
            {
                await Clients.Client(connectionId).SendAsync("ReceiveMessage", message);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send message: {ex}");
            }

            return false;
        }

        [Authorize]
        public async Task<bool> UpdateMessage(Message message)
        {
            if (message.Id == Guid.Empty || string.IsNullOrWhiteSpace(message.Content))
                return false;

            bool dbResult = await _messageRepository.UpdateMessage(message);
            if (!dbResult)
                return false;

            Message? updatedMessage = await _messageRepository.GetMessageByReferenceId(message.ReferenceId);
            if (updatedMessage == null)
                return false;

            UserDto[]? users = await GetChannelUsers(updatedMessage.ChannelId);
            if (users == null)
                return false;

            foreach (UserDto user in users)
            {
                string? connectionId = _connectionTracker.GetUserConnectionIds(user.Id).LastOrDefault();
                if (string.IsNullOrEmpty(connectionId))
                    continue;
                await Clients.Client(connectionId).SendAsync("UpdateMessage", updatedMessage);
            }

            return true;
        }

        [Authorize]
        public async Task SendPendingMessages()
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue)
                return;

            IEnumerable<UserPendingMessage> pendingMessages = await _messageRepository.GetPendingMessagesForUser(userId.Value);
            foreach (var pending in pendingMessages)
            {
                var message = await _messageRepository.GetMessageById(pending.MessageId);
                if (message == null)
                    continue;
                // Decrypt content if it was previously stored encrypted
                try { message.Content = _messageRepository.DecryptContent(pending.Content!); }
                catch { message.Content = pending.Content; }
                var userDto = new UserDto { Id = userId.Value };
                await SendClientMessage(userDto, message, ignorePendingMessages: true);
            }
        }

        [Authorize]
        public async Task UpdatePendingMessage(Guid messageid)
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue)
                return;

            await _messageRepository.UpdatePendingMessage(messageid);
        }
    }
}
