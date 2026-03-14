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
            if (!await CanSendMessageAsync(message))
                return false;

            bool dbResult = await _messageRepository.SendMessage(message);
            if (!dbResult)
                return false;

            UserDto[]? users = await GetChannelUsers(message.ChannelId);
            if (users == null)
                return false;

            await DeliverMessageToUsersAsync(users, message);
            await NotifyAgentsAsync(message);

            return true;
        }

        private async Task<bool> CanSendMessageAsync(Message? message)
        {
            if (message is null)
            {
                return false;
            }

            var userId = GetUserIdFromContext();
            if (!userId.HasValue || userId.Value != message.SenderId)
            {
                return false;
            }

            if (!await ChannelContainUser(message.ChannelId, message.SenderId))
            {
                return false;
            }

            return HasTransportContent(message);
        }

        private static bool HasTransportContent(Message message)
        {
            return !string.IsNullOrWhiteSpace(message.Content)
                || message.RecipientContents?.Count > 0;
        }

        private async Task DeliverMessageToUsersAsync(IEnumerable<UserDto> users, Message message)
        {
            foreach (UserDto user in users)
            {
                string? deliveryContent = GetDeliveryContentForUser(message, user.Id);
                if (string.IsNullOrWhiteSpace(deliveryContent))
                {
                    continue;
                }

                await SendClientMessage(user, message, ignorePendingMessages: false, deliveryContent);
            }
        }

        private async Task NotifyAgentsAsync(Message message)
        {
            if (_agentChannelResponder is null)
            {
                return;
            }

            await _agentChannelResponder.PersistAgentRecipientContentsAsync(message);
            _ = Task.Run(() => _agentChannelResponder.ProcessMentionsAsync(message));
        }

        private async Task<bool> SendClientMessage(UserDto user, Message message, bool ignorePendingMessages, string deliveryContent)
        {
            if (string.IsNullOrWhiteSpace(deliveryContent))
                return false;

            UserPendingMessage pendingMsg = new()
            {
                UserId = user.Id,
                MessageId = message.Id,
                Content = deliveryContent,
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
                await Clients.Client(connectionId).SendAsync("ReceiveMessage", CloneForDelivery(message, deliveryContent));
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
                message.Content = pending.Content;
                var userDto = new UserDto { Id = userId.Value };
                await SendClientMessage(userDto, message, ignorePendingMessages: true, pending.Content ?? string.Empty);
            }
        }

        private static string? GetDeliveryContentForUser(Message message, Guid userId)
        {
            if (message.RecipientContents is { Count: > 0 })
            {
                return message.RecipientContents.FirstOrDefault(x => x.UserId == userId)?.Content;
            }

            return message.Content;
        }

        private static Message CloneForDelivery(Message source, string deliveryContent)
        {
            return new Message
            {
                Id = source.Id,
                SenderId = source.SenderId,
                ChannelId = source.ChannelId,
                ReferenceId = source.ReferenceId,
                DateSent = source.DateSent,
                DateSeen = source.DateSeen,
                DateCreated = source.DateCreated,
                DateUpdated = source.DateUpdated,
                DateDeleted = source.DateDeleted,
                AgentDefinitionId = source.AgentDefinitionId,
                DisplayName = source.DisplayName,
                Content = deliveryContent,
            };
        }

        [Authorize]
        public async Task UpdatePendingMessage(Guid messageid)
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue)
                return;

            await _messageRepository.UpdatePendingMessage(messageid);
        }

        [Authorize]
        public async Task StartTyping(Guid channelId)
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue || !await ChannelContainUser(channelId, userId.Value))
            {
                return;
            }

            var user = await _userRepository.GetUserPublicDetails(userId.Value);
            var typingState = new ChannelTypingState
            {
                ChannelId = channelId,
                UserId = userId.Value,
                DisplayName = user?.Username,
                IsAgent = false
            };

            await BroadcastTypingState(channelId, typingState, "TypingStarted", userId.Value);
        }

        [Authorize]
        public async Task StopTyping(Guid channelId)
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue || !await ChannelContainUser(channelId, userId.Value))
            {
                return;
            }

            var user = await _userRepository.GetUserPublicDetails(userId.Value);
            var typingState = new ChannelTypingState
            {
                ChannelId = channelId,
                UserId = userId.Value,
                DisplayName = user?.Username,
                IsAgent = false
            };

            await BroadcastTypingState(channelId, typingState, "TypingStopped", userId.Value);
        }

        private async Task BroadcastTypingState(Guid channelId, ChannelTypingState typingState, string eventName, Guid? excludeUserId = null)
        {
            var users = await GetChannelUsers(channelId);
            if (users == null)
            {
                return;
            }

            foreach (var user in users)
            {
                if (excludeUserId.HasValue && user.Id == excludeUserId.Value)
                {
                    continue;
                }

                var connectionIds = _connectionTracker.GetUserConnectionIds(user.Id);
                if (connectionIds.Count == 0)
                {
                    continue;
                }

                await Clients.Clients(connectionIds).SendAsync(eventName, typingState);
            }
        }
    }
}
