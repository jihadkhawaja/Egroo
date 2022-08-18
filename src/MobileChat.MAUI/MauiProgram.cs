using MobileChat.Client.Services;
using MobileChat.MAUI.Interfaces;
using MobileChat.MAUI.Models;
using MobileChat.MAUI.Services;
using MobileChat.Shared.Interfaces;
using MobileChat.Shared.Models;
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

            //general services
            builder.Services.AddSingleton<SessionStorage>();
            builder.Services.AddSingleton<ISaveFile, SaveFileService>();
            //chat services
            builder.Services.AddScoped<IChatHub, ChatService>();

            var app = builder.Build();

            //get local cached user credentials
            using (IServiceScope scope = app.Services.CreateScope())
            {
                var SaveFileService = scope.ServiceProvider.GetRequiredService<ISaveFile>();
                var SessionStorage = scope.ServiceProvider.GetRequiredService<SessionStorage>();

                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mobile Chat");
                SessionStorage.User = SaveFileService.ReadFromJsonFile<User>("user.json", path, true);
            }

            return app;
        }
    }
}