using MobileChat.Client;

namespace MobileChat.MAUI
{
    public partial class App : Application
    {
        // SignalR chat hub name (http://your-web-url/hubName)
        public const string hubName = "chathub";
#if DEBUG
        // Development
        // SignalR Web URL example (http://localhost:2736/ or server IP address with port) where the chat web app is hosted
        public const string hubConnectionURL = "http://localhost:5175/" + hubName;
#else
        // Production
        // SignalR Web URL example (https://www.domain.com/ or server IP address with port) where the chat web app is hosted
        public const string hubConnectionURL = "your address here" + hubName;
#endif

        public static CancellationTokenSource ConnectionCancellationTokenSource { get; private set; }
        public App()
        {
            // Initialize client chat signalr service with your server chat hub url
            ClientChat.Initialize(hubConnectionURL);

            InitializeComponent();

            MainPage = new MainPage();
        }

        protected override async void OnStart()
        {
            await Connect();
        }

        public static async Task Connect()
        {
            //connect to the server through SignalR chathub
            ConnectionCancellationTokenSource = new();
            if (await ClientChat.SignalR.Connect(ConnectionCancellationTokenSource))
            {
                //client connected
            }
        }

        public static async Task Disconnect()
        {
            //disconnect from the server through SignalR chathub
            if (await ClientChat.SignalR.Disconnect())
            {
                //client disconnected
            }
        }
    }
}