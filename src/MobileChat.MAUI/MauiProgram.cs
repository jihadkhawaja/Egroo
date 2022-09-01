using MobileChat.Client.Services;
using MobileChat.MAUI.Interfaces;
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
            builder.Services.AddScoped<ChatService>();
            builder.Services.AddScoped<IChatAuth>(x => x.GetRequiredService<ChatService>());
            builder.Services.AddScoped<IChatUser>(x => x.GetRequiredService<ChatService>());
            builder.Services.AddScoped<IChatChannel>(x => x.GetRequiredService<ChatService>());
            builder.Services.AddScoped<IChatMessage>(x => x.GetRequiredService<ChatService>());

            MauiApp app = builder.Build();

            //get local cached user credentials
            using (IServiceScope scope = app.Services.CreateScope())
            {
                ISaveFile SaveFileService = scope.ServiceProvider.GetRequiredService<ISaveFile>();
                SessionStorage SessionStorage = scope.ServiceProvider.GetRequiredService<SessionStorage>();

                //https://docs.microsoft.com/en-us/dotnet/maui/platform-integration/storage/file-system-helpers
                SessionStorage.AppDataPath = Path.Combine(FileSystem.Current.AppDataDirectory, "Mobile Chat");

                try
                {
                    SessionStorage.User = SaveFileService.ReadFromJsonFile<User>("user.json", SessionStorage.AppDataPath, true);
                }
                catch { }
            }

            return app;
        }
    }
}