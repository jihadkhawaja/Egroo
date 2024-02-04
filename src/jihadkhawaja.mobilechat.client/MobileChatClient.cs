using jihadkhawaja.mobilechat.client.Core;
using jihadkhawaja.mobilechat.client.Interfaces;
using jihadkhawaja.mobilechat.client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace jihadkhawaja.mobilechat.client
{
    public static class MobileChatClient
    {
        public static IServiceCollection AddMobileChatServices(this IServiceCollection services)
        {
            //chat services
            services.AddScoped<IChatAuth, ChatAuthService>();
            services.AddScoped<IChatUser, ChatUserService>();
            services.AddScoped<IChatChannel, ChatChannelService>();
            services.AddScoped<IChatMessage, ChatMessageService>();

            return services;
        }

        /// <summary>
        /// MobileChat SignalR Abstraction
        /// </summary>
        public static SignalR? SignalR { get; private set; }
        public static void Initialize(string url, string token = "")
        {
            SignalR = new(url, token);
        }
    }
}
