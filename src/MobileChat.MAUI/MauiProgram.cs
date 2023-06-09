using jihadkhawaja.mobilechat.client.Models;
using jihadkhawaja.mobilechat.client.Services.Extension;
using MobileChat.UI.Constants;
using MobileChat.UI.Core;
using MobileChat.UI.Interfaces;
using MobileChat.UI.Services;
using MudBlazor.Services;

namespace MobileChat.MAUI
{
    public static class MauiProgram
    {
        public static CancellationTokenSource ConnectionCancellationTokenSource { get; private set; }
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
            builder.Services.AddMobileChatServices();

            MauiApp app = builder.Build();

            ConfigureApplication(app);

            return app;
        }

        private static void ConfigureApplication(MauiApp app)
        {
            //setup cache and try authenticate user
            using (IServiceScope scope = app.Services.CreateScope())
            {
                ISaveFile SaveFileService = scope.ServiceProvider.GetRequiredService<ISaveFile>();
                SessionStorage SessionStorage = scope.ServiceProvider.GetRequiredService<SessionStorage>();

                //https://docs.microsoft.com/en-us/dotnet/maui/platform-integration/storage/file-system-helpers
                SessionStorage.AppDataPath = Path.Combine(FileSystem.Current.AppDataDirectory, "Mobile Chat");

                try
                {
                    SessionStorage.User = SaveFileService.ReadFromJsonFile<User>("credentials", SessionStorage.AppDataPath, true);
                }
                catch { }

                HubInitializer.Initialize(Source.HubConnectionURL, SessionStorage.User?.Token);
            }
        }
    }
}