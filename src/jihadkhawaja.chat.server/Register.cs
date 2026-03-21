using System.Diagnostics.CodeAnalysis;
using jihadkhawaja.chat.server.Hubs;
using jihadkhawaja.chat.server.Services;
using jihadkhawaja.chat.shared.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace jihadkhawaja.chat.server
{
    [ExcludeFromCodeCoverage]
    public static class Register
    {
        /// <summary>
        /// Adds the chat SignalR hub and its required services.
        /// Before calling this, register your <see cref="IAuth"/>, <see cref="IUser"/>,
        /// <see cref="IChannel"/>, and <see cref="IMessageRepository"/> implementations.
        /// <see cref="IConnectionTracker"/> is registered automatically with the built-in
        /// in-memory implementation; override it beforehand if you need a different backend
        /// (e.g. Redis for distributed deployments).
        /// </summary>
        public static IServiceCollection AddChatHub(this IServiceCollection services, Action<Microsoft.AspNetCore.SignalR.HubOptions>? configureHub = null)
        {
            // Register a default IConnectionTracker only when the consumer has not already provided one.
            services.TryAddSingleton<IConnectionTracker, InMemoryConnectionTracker>();

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
