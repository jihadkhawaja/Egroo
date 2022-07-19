using Microsoft.AspNetCore.SignalR.Client;

namespace JihadKhawaja.SignalR.Client.Chat.Core
{
    public static class SignalR
    {
        public static HubConnection HubConnection { get; private set; }

        public static event Func<Exception, Task> Reconnecting;
        public static event Func<string, Task> Reconnected;
        public static event Func<Exception, Task> Closed;
        public static bool Initialize(string url)
        {
            try
            {
                HubConnection = new HubConnectionBuilder()
                .WithAutomaticReconnect(new TimeSpan[5]
                {
                    new TimeSpan(0,0,0),
                    new TimeSpan(0,0,5),
                    new TimeSpan(0,0,10),
                    new TimeSpan(0,0,30),
                    new TimeSpan(0,0,60)
                })
                .WithUrl(url)
                .Build();

                SubscribeHubEvents();

                return true;
            }
            catch { }

            return false;
        }
        private static void SubscribeHubEvents()
        {
            HubConnection.Reconnected += HubConnection_Reconnected;
            HubConnection.Reconnecting += HubConnection_Reconnecting;
            HubConnection.Closed += HubConnection_Closed;
        }

        private static Task HubConnection_Reconnecting(Exception arg)
        {
            Reconnecting?.Invoke(arg);

            return Task.CompletedTask;
        }

        private static Task HubConnection_Reconnected(string arg)
        {
            Reconnected?.Invoke(arg);

            return Task.CompletedTask;
        }

        private static Task HubConnection_Closed(Exception arg)
        {
            Closed?.Invoke(arg);

            return Task.CompletedTask;
        }

        public static async Task<bool> Connect(CancellationTokenSource cts)
        {
            try
            {
                await HubConnection.StartAsync(cts.Token);

                //return true on success and false if cancelled
                if (cts.IsCancellationRequested)
                {
                    return false;
                }
                else
                {
                    if (HubConnection.State == HubConnectionState.Connected)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch { }

            return false;
        }
        public static async Task<bool> Disconnect()
        {
            try
            {
                await HubConnection.StopAsync();

                return true;
            }
            catch { }

            return false;
        }
    }
}
