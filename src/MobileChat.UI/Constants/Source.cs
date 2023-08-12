namespace MobileChat.UI.Constants
{
    public static class Source
    {
        // SignalR chat hub name (http://your-web-url/hubName)
        public const string HubName = "chathub";

#if DEBUG
        // Development
        // SignalR Web URL example (http://localhost:2736/ or server IP address with port) where the chat web app is hosted
        public const string ConnectionURL = "http://localhost:5175/";
#else
        // Production
        // SignalR Web URL example (https://www.domain.com/ or server IP address with port) where the chat web app is hosted
        public const string ConnectionURL = "https://api.egroo.org/";
#endif

        public const string HubConnectionURL = ConnectionURL + HubName;
    }
}
