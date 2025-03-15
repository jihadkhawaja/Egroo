using jihadkhawaja.chat.server.Database;
using jihadkhawaja.chat.server.Security;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace jihadkhawaja.chat.server.Repository
{
    public class MessageRepository : BaseRepository, IMessage
    {
        public MessageRepository(DataContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            EncryptionService encryptionService)
            : base(dbContext, httpContextAccessor, configuration, encryptionService)
        {
        }

        public async Task<bool> SendMessage(Message message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.Content))
                return false;

            // Encrypt and set properties.
            message.Content = _encryptionService.Encrypt(message.Content);
            message.Id = Guid.NewGuid();
            message.DateSent = DateTime.UtcNow;
            message.IsEncrypted = true;

            try
            {
                await _dbContext.Messages.AddAsync(message);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch
            {
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
            catch
            {
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
            catch
            {
                // Optionally log error
            }
        }

        // Helper methods for internal use

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
            catch
            {
                return false;
            }
        }
    }
}
