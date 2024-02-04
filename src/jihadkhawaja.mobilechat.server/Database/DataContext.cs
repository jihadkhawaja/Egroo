using Microsoft.Extensions.Configuration;

namespace jihadkhawaja.mobilechat.server.Database
{
    public class DataContext : MobileChatDataContext
    {
        public DataContext(IConfiguration configuration) : base(configuration)
        {
        }
    }
}
