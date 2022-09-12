using MobileChat.Client.Core;

namespace MobileChat.Client
{
    public static class Connection
    {
        public static SignalR SignalR { get; private set; }
        public static void Initialize(string url, string token = null)
        {
            SignalR = new(url, token);
        }
    }
}