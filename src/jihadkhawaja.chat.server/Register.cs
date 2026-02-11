using jihadkhawaja.chat.server.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace jihadkhawaja.chat.server
{
    public static class Register
    {
        /// <summary>
        /// Adds the chat SignalR hub services.
        /// Call this after registering your own IAuth, IUser, IChannel, IMessageRepository, and IConnectionTracker implementations.
        /// </summary>
        public static IServiceCollection AddChatHub(this IServiceCollection services, Action<Microsoft.AspNetCore.SignalR.HubOptions>? configureHub = null)
        {
            if (configureHub != null)
            {
                services.AddSignalR(configureHub);
            }
            else
            {
                services.AddSignalR(options =>
                {
                    options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
                });
            }

            return services;
        }

        /// <summary>
        /// Maps the ChatHub SignalR endpoint.
        /// </summary>
        public static WebApplication MapChatHub(this WebApplication app, string pattern = "/chathub")
        {
            app.MapHub<ChatHub>(pattern, options =>
            {
                options.Transports = HttpTransportType.WebSockets;
            });

            return app;
        }
    }
}
