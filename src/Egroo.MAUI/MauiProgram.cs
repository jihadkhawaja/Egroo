using Egroo.UI;
using Egroo.UI.Models;

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