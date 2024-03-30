using Egroo.UI;
using Egroo.UI.Models;
using Microsoft.Extensions.Logging;

namespace Egroo.MAUI
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
            builder.Logging.AddDebug();
#endif

            //https://docs.microsoft.com/en-us/dotnet/maui/platform-integration/storage/file-system-helpers
            ClientModel.AppDataPath = Path.Combine(FileSystem.Current.AppDataDirectory, "Ergoo");

            ClientModel.MyMudThemeProvider = builder =>
            {
                builder.OpenComponent(0, typeof(MyMudThemeProvider));
                builder.CloseComponent();
            };

            ClientModel.MyMudProvider = builder =>
            {
                builder.OpenComponent(0, typeof(MyMudProviders));
                builder.CloseComponent();
            };

            builder.Services.AddEgrooServices(FrameworkPlatform.MAUI);

            MauiApp app = builder.Build();

            return app;
        }
    }
}