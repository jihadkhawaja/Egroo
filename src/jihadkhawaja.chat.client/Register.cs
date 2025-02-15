using BlazorDexie.Extensions;
using jihadkhawaja.chat.client.CacheDB;
using jihadkhawaja.chat.client.Services;
using jihadkhawaja.chat.shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace jihadkhawaja.chat.client
{
    public static class Register
    {
        public static IServiceCollection AddChatServices(this IServiceCollection services)
        {
            //chat services
            services.AddScoped<IChatAuth, ChatAuthService>();
            services.AddScoped<IChatUser, ChatUserService>();
            services.AddScoped<IChatChannel, ChatChannelService>();
            services.AddScoped<IChatMessage, ChatMessageService>();
            services.AddScoped<IChatCall, ChatCallService>();

            services.AddDexieWrapper();
            services.AddScoped<EgrooDB>();

            return services;
        }
    }
}
