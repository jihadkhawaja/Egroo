using MobileChat.Client.Core;

namespace MobileChat.Client
{
    public static class ClientChat
    {
        public static SignalR SignalR { get; private set; }
        public static void Initialize(string url)
        {
            SignalR = new(url);
        }
    }
}