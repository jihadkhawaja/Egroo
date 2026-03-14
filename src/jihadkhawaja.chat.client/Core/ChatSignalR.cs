using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics.CodeAnalysis;

namespace jihadkhawaja.chat.client.Core
{
    [ExcludeFromCodeCoverage]
    public static class ChatSignalR
    {
        public static HubConnection? HubConnection { get; private set; }

        public static void Initialize(string url, string? token = "")
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                // No token: use cookie-based auth (BFF pattern).
                // Skip the HTTP negotiate step and go straight to WebSockets so
                // the browser sends its session cookie on the WS upgrade request.
                HubConnection = new HubConnectionBuilder()
                    .WithAutomaticReconnect()
                    .WithUrl(url, options =>
                    {
                        options.Transports = HttpTransportType.WebSockets;
                        options.SkipNegotiation = true;
                    })
                    .Build();
            }
            else
            {
                HubConnection = new HubConnectionBuilder()
                    .WithAutomaticReconnect()
                    .WithUrl(string.Format("{0}?access_token={1}", url, token), options =>
                    {
                        options.Transports = HttpTransportType.WebSockets;
                        options.SkipNegotiation = true;
                    })
                    .Build();
            }
        }
    }
}
