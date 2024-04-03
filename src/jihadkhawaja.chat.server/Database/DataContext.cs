using Microsoft.Extensions.Configuration;

namespace jihadkhawaja.chat.server.Database
{
    public class DataContext : MobileChatDataContext
    {
        public DataContext(IConfiguration configuration) : base(configuration)
        {
        }
    }
}
