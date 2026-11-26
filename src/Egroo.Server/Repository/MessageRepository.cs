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

        public async Task<bool> UpdateMessage(Message message)
        {
            if (message.Id == Guid.Empty || string.IsNullOrWhiteSpace(message.Content))
                return false;

            Message? dbMessage = await _dbContext.Messages.FirstOrDefaultAsync(x => x.ReferenceId == message.ReferenceId);
            if (dbMessage is null)
                return false;

            dbMessage.DateSeen = DateTimeOffset.UtcNow;
            dbMessage.DateUpdated = DateTimeOffset.UtcNow;

            try
            {
                _dbContext.Messages.Update(dbMessage);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating message");
                return false;
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
