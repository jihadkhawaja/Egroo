using jihadkhawaja.mobilechat.client.Interfaces;
using jihadkhawaja.mobilechat.client.Models;
using jihadkhawaja.mobilechat.client.Services;
using MobileChat.MAUI.Interfaces;
using MobileChat.MAUI.Services;
using jihadkhawaja.mobilechat.client;
using MudBlazor.Services;
using jihadkhawaja.mobilechat.client.Services.Extension;

namespace MobileChat.MAUI
{
    public static class MauiProgram
    {
        // SignalR chat hub name (http://your-web-url/hubName)
        private const string HubName = "chathub";
#if DEBUG
        // Development
        // SignalR Web URL example (http://localhost:2736/ or server IP address with port) where the chat web app is hosted
        public const string HubConnectionURL = "http://localhost:5175/" + HubName;
#else
        // Production
        // SignalR Web URL example (https://www.domain.com/ or server IP address with port) where the chat web app is hosted
        public const string HubConnectionURL = "your address here" + HubName;
#endif
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

                Initialize(HubConnectionURL, SessionStorage.User?.Token);
            }
        }

        public static void Initialize(string HubConnectionURL, string Token = "")
        {
            // Initialize client chat signalr service with your server chat hub url
            jihadkhawaja.mobilechat.client.MobileChat.Initialize(HubConnectionURL, Token);
        }

        public static async Task Connect()
        {
            //connect to the server through SignalR chathub
            ConnectionCancellationTokenSource = new();
            if (await jihadkhawaja.mobilechat.client.MobileChat.SignalR.Connect(ConnectionCancellationTokenSource))
            {
                //client connected
            }
        }

        public static async Task Disconnect()
        {
            //disconnect from the server through SignalR chathub
            if (await jihadkhawaja.mobilechat.client.MobileChat.SignalR.Disconnect())
            {
                //client disconnected
            }
        }
    }
}