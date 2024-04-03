using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.chat.client.Core
{
    public static class MobileChatSignalR
    {
        public static HubConnection? HubConnection { get; private set; }

        public static void Initialize(string url, string? token = "")
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                HubConnection = new HubConnectionBuilder()
                .WithAutomaticReconnect()
                .WithUrl(url)
                .Build();
            }
            else
            {
                HubConnection = new HubConnectionBuilder()
                .WithAutomaticReconnect()
                .WithUrl(string.Format("{0}?access_token={1}", url, token))
                .Build();
            }
        }
    }
}
