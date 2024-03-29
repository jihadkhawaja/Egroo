using Egroo.UI.Interfaces;
using Egroo.UI.Models;
using Egroo.UI.Services;
using jihadkhawaja.mobilechat.client;
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
            services.AddMudServices();

            //general services
            if (frameworkPlatform == FrameworkPlatform.SERVER)
            {
                services.AddScoped<SessionStorage>();
                services.AddScoped<LocalStorageService>();
                services.AddScoped<ISaveFile, SaveFileService>();
            }
            else
            {
                services.AddSingleton<SessionStorage>();
                services.AddSingleton<LocalStorageService>();
                services.AddSingleton<ISaveFile, SaveFileService>();
            }

            services.AddMobileChatServices();

            return services;
        }
    }
}
