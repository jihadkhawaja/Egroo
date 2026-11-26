using Egroo.Server.Database;
using Egroo.Server.Security;
using Egroo.Contracts.Interfaces;
using Egroo.Contracts.Models;
using Microsoft.EntityFrameworkCore;

namespace Egroo.Server.Repository
{
    public class MessageRepository : BaseRepository, IMessageRepository
    {
        private readonly EncryptionService _encryptionService;

        public MessageRepository(DataContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            IConnectionTracker connectionTracker,
            EncryptionService encryptionService,
            ILogger<MessageRepository> logger)
            : base(dbContext, httpContextAccessor, configuration, connectionTracker, logger)
        {
            _encryptionService = encryptionService;
        }

        public string DecryptContent(string content) => _encryptionService.Decrypt(content);

        public Task<bool> SendMessage(Message message) => SendMessage(message, []);

        public async Task<bool> SendMessage(Message message, IReadOnlyCollection<MessageRecipientContent> deliveries)
        {
            bool hasTransportContent = !string.IsNullOrWhiteSpace(message?.Content)
                || (message?.RecipientContents?.Count > 0)
                || (message?.AgentRecipientContents?.Count > 0);

            if (message == null || !hasTransportContent)
                return false;

            message.Id = Guid.NewGuid();
            message.DateSent = DateTimeOffset.UtcNow;
            var pendingMessages = deliveries.Select(delivery => new UserPendingMessage
            {
                UserId = delivery.UserId,
                MessageId = message.Id,
                Content = delivery.Content,
            }).ToArray();

            try
            {
                _dbContext.Messages.Add(message);
                _dbContext.UsersPendingMessages.AddRange(pendingMessages);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _dbContext.Entry(message).State = EntityState.Detached;
                foreach (var pendingMessage in pendingMessages)
                {
                    _dbContext.Entry(pendingMessage).State = EntityState.Detached;
                }
                _logger.LogError(ex, "Error sending message");
                return false;
            }
        }

        public async Task<(Message? Message, bool Changed)> MarkMessageSeen(Guid readerId, Guid messageId, Guid referenceId)
        {
            var authorizedMessage = _dbContext.Messages.Where(message =>
                message.Id == messageId && message.ReferenceId == referenceId && message.DateDeleted == null
                && message.SenderId != readerId && _dbContext.ChannelUsers.Any(member =>
                    member.UserId == readerId && member.ChannelId == message.ChannelId && member.DateDeleted == null));

            try
            {
                var seenAt = DateTimeOffset.UtcNow;
                int changed = await authorizedMessage.Where(message => message.DateSeen == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(message => message.DateSeen, seenAt)
                        .SetProperty(message => message.DateUpdated, seenAt));

                return (await authorizedMessage.AsNoTracking().SingleOrDefaultAsync(), changed == 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording a read receipt for message {MessageId}", messageId);
                return default;
            }
        }

        public async Task UpdatePendingMessage(Guid userId, Guid messageId)
        {
            try
            {
                var pendingMessages = await _dbContext.UsersPendingMessages
                    .Where(x => x.UserId == userId
                        && x.MessageId == messageId
                        && x.DateUserReceivedOn == null
                        && x.DateDeleted == null)
                    .ToListAsync();

                if (pendingMessages.Count == 0)
                    return;

                _dbContext.UsersPendingMessages.RemoveRange(pendingMessages);
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another receipt already removed the pending delivery.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating pending message");
            }
        }

        #region helper methods
        public async Task<Message?> GetMessageByReferenceId(Guid referenceId)
        {
            return await _dbContext.Messages.FirstOrDefaultAsync(x => x.ReferenceId == referenceId);
        }

        public async Task<IEnumerable<UserPendingMessage>> GetPendingMessagesForUser(Guid userId)
        {
            return await _dbContext.UsersPendingMessages.Where(x => x.UserId == userId).ToListAsync();
        }

        public async Task<IReadOnlyList<(Message Message, string? Content)>> GetPendingMessageDeliveries(Guid userId)
        {
            var deliveries = await (
                from pending in _dbContext.UsersPendingMessages
                join message in _dbContext.Messages on pending.MessageId equals message.Id
                where pending.UserId == userId && pending.DateUserReceivedOn == null
                    && pending.DateDeleted == null && message.DateDeleted == null
                    && pending.Content != null
                orderby message.DateSent ?? message.DateCreated, message.Id, pending.Id
                select new
                {
                    Message = message,
                    pending.Content,
                    AgentName = _dbContext.AgentDefinitions
                        .Where(agent => agent.Id == message.AgentDefinitionId)
                        .Select(agent => agent.Name).FirstOrDefault()
                }
            ).AsNoTracking().ToArrayAsync();

            foreach (var delivery in deliveries)
            {
                delivery.Message.DisplayName = delivery.AgentName;
            }

            return deliveries.Select(delivery => (delivery.Message, delivery.Content)).ToArray();
        }

        public async Task<Message?> GetMessageById(Guid messageId)
        {
            return await _dbContext.Messages.FirstOrDefaultAsync(x => x.Id == messageId);
        }

        public async Task<bool> AddPendingMessage(UserPendingMessage pendingMessage)
        {
            try
            {
                await _dbContext.UsersPendingMessages.AddAsync(pendingMessage);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding pending message");
                return false;
            }
        }
        #endregion
    }
}
