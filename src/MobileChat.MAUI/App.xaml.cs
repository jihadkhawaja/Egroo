using JihadKhawaja.SignalR.Client.Chat.Core;

namespace MobileChat.MAUI
{
    public partial class App : Application
    {
        // SignalR chat hub name (http://your-web-url/hubName)
        public const string hubName = "chathub";
#if DEBUG
        // Development
        // SignalR Web URL example (http://localhost:2736/) where the chat web app is hosted
        public const string hubConnectionURL = "http://192.168.0.106:45456/" + hubName;
#else
        //production
        //SignalR Web URL example (https://www.domain.com/) where the chat web app is hosted
        public const string hubConnectionURL = "your address here" + hubName;
#endif
        public App()
        {
            // Initialize client chat signalr service with your server chat hub url
            SignalR.Initialize(hubConnectionURL);

            InitializeComponent();

            MainPage = new MainPage();
        }
    }
}