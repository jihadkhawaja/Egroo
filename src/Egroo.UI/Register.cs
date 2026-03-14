using BlazorDexie.Extensions;
using Egroo.UI.CacheDB;
using Egroo.UI.Models;
using Egroo.UI.Services;
using jihadkhawaja.chat.client;
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

            services.AddScoped<SessionStorage>();
            services.AddScoped<StorageService>();
            services.AddScoped<EndToEndEncryptionService>();

            services.AddBlazorDexie();
            services.AddScoped<EgrooDB>();

            services.AddMudServices();
            services.AddChatServices();

            return services;
        }
    }
}
