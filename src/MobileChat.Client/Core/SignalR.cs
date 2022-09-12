using Microsoft.AspNetCore.SignalR.Client;

namespace MobileChat.Client.Core
{
    public class SignalR
    {
        public HubConnection HubConnection { get; private set; }

        public event Func<Exception, Task> Reconnecting;
        public event Func<string, Task> Reconnected;
        public event Func<Exception, Task> Closed;

        public SignalR(string url, string token = null)
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

            SubscribeHubEvents();
        }

        private void SubscribeHubEvents()
        {
            HubConnection.Reconnected += HubConnection_Reconnected;
            HubConnection.Reconnecting += HubConnection_Reconnecting;
            HubConnection.Closed += HubConnection_Closed;
        }

        private Task HubConnection_Reconnecting(Exception arg)
        {
            Reconnecting?.Invoke(arg);

            return Task.CompletedTask;
        }

        private Task HubConnection_Reconnected(string arg)
        {
            Reconnected?.Invoke(arg);

            return Task.CompletedTask;
        }

        private Task HubConnection_Closed(Exception arg)
        {
            Closed?.Invoke(arg);

            return Task.CompletedTask;
        }

        public async Task<bool> Connect(CancellationTokenSource cts = default)
        {
            try
            {
                if (cts is not null)
                {
                    await HubConnection.StartAsync(cts.Token);
                }
                else
                {
                    await HubConnection.StartAsync();
                }

                //return true on success and false if cancelled
                if (cts is not null && cts.IsCancellationRequested)
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
        public async Task<bool> Disconnect()
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
