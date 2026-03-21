using System.Diagnostics.CodeAnalysis;
using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.chat.client.Services
{
    [ExcludeFromCodeCoverage]
    public class ChatMessageService : IMessageHub, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new();
        private bool _isSubscribed;
        private HubConnection? _subscribedConnection;

        public event Func<Message, Task>? OnMessageReceived;
        public event Func<Message, Task>? OnMessageUpdated;
        public event Func<ChannelTypingState, Task>? OnTypingStarted;
        public event Func<ChannelTypingState, Task>? OnTypingStopped;

        private HubConnection HubConnection => ChatSignalR.HubConnection
            ?? throw new NullReferenceException("SignalR not initialized");

        public ChatMessageService()
        {
            EnsureSubscriptions();
        }

        private void EnsureSubscriptions()
        {
            if (ChatSignalR.HubConnection is null)
            {
                return;
            }

            if (_isSubscribed && ReferenceEquals(_subscribedConnection, ChatSignalR.HubConnection))
            {
                return;
            }

            ResetSubscriptions();

            var connection = ChatSignalR.HubConnection;

            _subscriptions.Add(connection.On<Message>("ReceiveMessage", async message =>
            {
                if (OnMessageReceived != null)
                {
                    await OnMessageReceived.Invoke(message);
                }
            }));

            _subscriptions.Add(connection.On<Message>("UpdateMessage", async message =>
            {
                if (OnMessageUpdated != null)
                {
                    await OnMessageUpdated.Invoke(message);
                }
            }));

            _subscriptions.Add(connection.On<ChannelTypingState>("TypingStarted", async typingState =>
            {
                if (OnTypingStarted != null)
                {
                    await OnTypingStarted.Invoke(typingState);
                }
            }));

            _subscriptions.Add(connection.On<ChannelTypingState>("TypingStopped", async typingState =>
            {
                if (OnTypingStopped != null)
                {
                    await OnTypingStopped.Invoke(typingState);
                }
            }));

            _subscribedConnection = connection;
            _isSubscribed = true;
        }

        private void ResetSubscriptions()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
            _isSubscribed = false;
            _subscribedConnection = null;
        }

        public async Task<bool> SendMessage(Message message)
        {
            EnsureSubscriptions();
            return await HubConnection.InvokeAsync<bool>(nameof(SendMessage), message);
        }

        public async Task<bool> UpdateMessage(Message message)
        {
            EnsureSubscriptions();
            return await HubConnection.InvokeAsync<bool>(nameof(UpdateMessage), message);
        }

        public async Task SendPendingMessages()
        {
            EnsureSubscriptions();
            await HubConnection.InvokeAsync(nameof(SendPendingMessages));
        }

        public async Task UpdatePendingMessage(Guid messageid)
        {
            EnsureSubscriptions();
            await HubConnection.InvokeAsync(nameof(UpdatePendingMessage), messageid);
        }

        public async Task StartTyping(Guid channelId)
        {
            EnsureSubscriptions();
            await HubConnection.InvokeAsync(nameof(StartTyping), channelId);
        }

        public async Task StopTyping(Guid channelId)
        {
            EnsureSubscriptions();
            await HubConnection.InvokeAsync(nameof(StopTyping), channelId);
        }

        public void Dispose()
        {
            ResetSubscriptions();
        }
    }
}
