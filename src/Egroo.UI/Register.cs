using Egroo.UI.Models;
using Egroo.UI.Services;
using jihadkhawaja.chat.client;
using jihadkhawaja.chat.client.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Egroo.UI
{
    public static class Register
    {
        public static IServiceCollection AddEgrooServices(
            this IServiceCollection services,
            FrameworkPlatform frameworkPlatform)
        {
            ClientModel.CurrentFrameworkPlatform = frameworkPlatform;

            if (frameworkPlatform == FrameworkPlatform.SERVER)
            {
                services.AddScoped<SessionStorage>();
                services.AddScoped<StorageService>();
            }
            else
            {
                services.AddSingleton<SessionStorage>();
                services.AddSingleton<StorageService>();
            }

            services.AddMudServices();
            services.AddChatServices();

            return services;
        }
    }
}
