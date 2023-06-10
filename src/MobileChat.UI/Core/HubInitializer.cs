namespace MobileChat.UI.Core
{
    public static class HubInitializer
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
        public const string HubConnectionURL = "https://www.your-domain.com/" + HubName;
#endif
        public static CancellationTokenSource ConnectionCancellationTokenSource { get; private set; }

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

        public static bool IsConnected()
        {
            return jihadkhawaja.mobilechat.client.MobileChat.SignalR.HubConnection.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected;
        }
    }
}
