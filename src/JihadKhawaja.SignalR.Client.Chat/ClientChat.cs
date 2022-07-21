namespace JihadKhawaja.SignalR.Client.Chat
{
    public static class ClientChat
    {
        public static Core.SignalR SignalR { get; private set; }
        public static void Initialize(string url)
        {
            SignalR = new(url);
        }
    }
}