using Egroo.Contracts.Interfaces;
using Egroo.Contracts.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Egroo.Server.SignalR.Hubs
{
    public partial class ChatHub : IMessageHub
    {
        [Authorize]
        public async Task<bool> SendMessage(Message message)
        {
            if (!await CanSendMessageAsync(message))
                return false;

            UserDto[]? users = await GetChannelUsers(message.ChannelId);
            if (users is null)
                return false;

            var deliveries = GetUserDeliveries(users, message);
            if (deliveries.Count == 0 || !await _messageRepository.SendMessage(message, deliveries))
                return false;

            foreach (var delivery in deliveries)
            {
                await SendClientMessage(delivery.UserId, message, delivery.Content);
            }

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

        private static List<MessageRecipientContent> GetUserDeliveries(UserDto[] users, Message message)
        {
            var deliveries = new List<MessageRecipientContent>(users.Length);
            foreach (var user in users)
            {
                string? deliveryContent = GetDeliveryContentForUser(message, user.Id);
                if (!string.IsNullOrWhiteSpace(deliveryContent))
                {
                    deliveries.Add(new MessageRecipientContent { UserId = user.Id, Content = deliveryContent });
                }
            }

            return deliveries;
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

        private async Task SendClientMessage(Guid userId, Message message, string? deliveryContent)
        {
            if (string.IsNullOrWhiteSpace(deliveryContent))
                return;

            var connectionIds = _connectionTracker.GetUserConnectionIds(userId);
            if (connectionIds.Count == 0)
                return;

            try
            {
                await Clients.Clients(connectionIds).SendAsync("ReceiveMessage", CloneForDelivery(message, deliveryContent));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send message: {ex}");
            }
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

            var deliveries = await _messageRepository.GetPendingMessageDeliveries(userId.Value);
            foreach (var (message, content) in deliveries)
            {
                if (!string.IsNullOrWhiteSpace(content))
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", CloneForDelivery(message, content), Context.ConnectionAborted);
                }
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

            await _messageRepository.UpdatePendingMessage(userId.Value, messageid);
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
