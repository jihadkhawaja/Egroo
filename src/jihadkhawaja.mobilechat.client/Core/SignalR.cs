using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.mobilechat.client.Core
{
    public class SignalR
    {
        public HubConnection HubConnection { get; private set; }

        public event Func<Exception, Task>? Reconnecting;
        public event Func<string, Task>? Reconnected;
        public event Func<Exception, Task>? Closed;

        public SignalR(string url, string token = "")
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

        private Task HubConnection_Reconnecting(Exception? arg)
        {
            if (arg is null)
            {
                return Task.CompletedTask;
            }

            Reconnecting?.Invoke(arg);

            return Task.CompletedTask;
        }

        private Task HubConnection_Reconnected(string? arg)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                return Task.CompletedTask;
            }

            Reconnected?.Invoke(arg);

            return Task.CompletedTask;
        }

        private Task HubConnection_Closed(Exception? arg)
        {
            if (arg is null)
            {
                return Task.CompletedTask;
            }

            Closed?.Invoke(arg);

            return Task.CompletedTask;
        }
        /// <summary>
        /// Connect to the server
        /// </summary>
        /// <param name="cts"></param>
        /// <returns>true when connected successfully</returns>
        public async Task<bool> Connect(CancellationTokenSource? cts = default)
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
        /// <summary>
        /// Disconnect from the connected server
        /// </summary>
        /// <returns>true when fully disconnected</returns>
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
