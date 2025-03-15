using jihadkhawaja.chat.server.Database;
using jihadkhawaja.chat.server.Security;
using Microsoft.Extensions.Configuration;

namespace jihadkhawaja.chat.server.Repository
{
    public abstract class BaseRepository
    {
        protected readonly DataContext _dbContext;
        protected readonly IConfiguration _configuration;
        protected readonly EncryptionService _encryptionService;

        protected BaseRepository(DataContext dbContext, 
            IConfiguration configuration, 
            EncryptionService encryptionService)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _encryptionService = encryptionService;
        }
    }
}
