using Microsoft.Extensions.Logging;

namespace Egroo.UI.Helpers
{
    public static class ClientLoggingConfiguration
    {
        public static void Configure(ILoggingBuilder logging)
        {
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddFilter("BlazorDexie", LogLevel.Warning);
            logging.AddFilter("BlazorDexie.Database", LogLevel.Warning);
            logging.AddFilter("Microsoft.AspNetCore.SignalR.Client", LogLevel.Warning);
            logging.AddFilter("Microsoft.AspNetCore.Http.Connections.Client", LogLevel.Warning);
        }
    }
}