using Egroo.UI.Constants;
using Egroo.UI.Core;
using Egroo.UI.Interfaces;
using Egroo.UI.Models;
using Egroo.UI.Services;
using jihadkhawaja.mobilechat.client;
using jihadkhawaja.mobilechat.client.Models;
using MudBlazor.Services;

namespace Egroo.MAUI
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
            builder.Services.AddSingleton<LocalStorageService>();
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
                    SessionStorage.CurrentFrameworkPlatform = FrameworkPlatform.MAUI;
                    SessionStorage.User = SaveFileService.ReadFromJsonFile<User>("credentials", SessionStorage.AppDataPath, true);
                }
                catch { }

                HubInitializer.Initialize(Source.HubConnectionURL, SessionStorage.User?.Token);
            }
        }
    }
}