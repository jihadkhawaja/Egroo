using Egroo.Server.Database;
using Egroo.Server.Security;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
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

        public async Task<bool> SendMessage(Message message)
        {
            bool hasTransportContent = !string.IsNullOrWhiteSpace(message?.Content)
                || (message?.RecipientContents?.Count > 0);

            if (message == null || !hasTransportContent)
                return false;

            message.Id = Guid.NewGuid();
            message.DateSent = DateTime.UtcNow;

            try
            {
                await _dbContext.Messages.AddAsync(message);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
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

        public async Task UpdatePendingMessage(Guid messageid)
        {
            try
            {
                var pendingMessage = await _dbContext.UsersPendingMessages.FirstOrDefaultAsync(x =>
                    x.MessageId == messageid &&
                    x.DateUserReceivedOn == null &&
                    x.DateDeleted == null);

                if (pendingMessage is not null)
                {
                    _dbContext.UsersPendingMessages.Remove(pendingMessage);
                    await _dbContext.SaveChangesAsync();
                }
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
