using System.Diagnostics.CodeAnalysis;
using Egroo.Client.Services;
using Egroo.Contracts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Egroo.Client
{
    [ExcludeFromCodeCoverage]
    public static class Register
    {
        public static IServiceCollection AddChatServices(this IServiceCollection services)
        {
            //chat services
            services.AddScoped<IAuthClient, AuthService>();
            services.AddScoped<IUser, ChatUserService>();
            services.AddScoped<IChannel, ChatChannelService>();
            services.AddScoped<ChatMessageService>();
            services.AddScoped<IMessageHub>(sp => sp.GetRequiredService<ChatMessageService>());
            services.AddScoped<ChatCallService>();
            services.AddScoped<AgentService>();
            services.AddScoped<ChannelFileService>();

            return services;
        }
    }
}
