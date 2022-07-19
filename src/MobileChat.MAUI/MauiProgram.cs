using JihadKhawaja.SignalR.Client.Chat.Interfaces;
using JihadKhawaja.SignalR.Client.Chat.Services;
using MudBlazor.Services;

namespace MobileChat.MAUI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            MauiAppBuilder builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
#endif
            builder.Services.AddMudServices();

            //chat services
            builder.Services.AddScoped<IChat, ChatService>();

            return builder.Build();
        }
    }
}