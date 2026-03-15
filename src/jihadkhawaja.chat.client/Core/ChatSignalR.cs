using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics.CodeAnalysis;

namespace jihadkhawaja.chat.client.Core
{
    [ExcludeFromCodeCoverage]
    public static class ChatSignalR
    {
        public static readonly TimeSpan[] AutomaticReconnectDelays =
        [
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30)
        ];

        public static HubConnection? HubConnection { get; private set; }

        public static TimeSpan GetAutomaticReconnectElapsedDelay(int retryCount)
        {
            if (retryCount <= 0)
            {
                return TimeSpan.Zero;
            }

            long totalTicks = 0;
            int appliedRetryCount = Math.Min(retryCount, AutomaticReconnectDelays.Length);

            for (int index = 0; index < appliedRetryCount; index++)
            {
                totalTicks += AutomaticReconnectDelays[index].Ticks;
            }

            return TimeSpan.FromTicks(totalTicks);
        }

        public static void Initialize(string url, string? token = "")
        {
            HubConnection = new HubConnectionBuilder()
                .WithAutomaticReconnect(AutomaticReconnectDelays)
                .WithUrl(url, options =>
                {
                    options.Transports = HttpTransportType.WebSockets;
                    options.SkipNegotiation = true;

                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    }
                })
                .Build();
        }
    }
}
