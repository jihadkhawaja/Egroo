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
            services.AddScoped<IAuth, AuthService>();
            services.AddScoped<IUser, ChatUserService>();
            services.AddScoped<IChannel, ChatChannelService>();
            services.AddScoped<IMessageHub, ChatMessageService>();
            services.AddScoped<ChatCallService>();

            services.AddDexieWrapper();
            services.AddScoped<EgrooDB>();

            return services;
        }
    }
}
