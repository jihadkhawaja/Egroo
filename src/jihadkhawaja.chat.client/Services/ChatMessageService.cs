using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.chat.client.Services
{
    public class ChatMessageService : IMessageHub
    {
        private HubConnection HubConnection => MobileChatSignalR.HubConnection
            ?? throw new NullReferenceException("SignalR not initialized");

        public async Task<bool> SendMessage(Message message)
            => await HubConnection.InvokeAsync<bool>(nameof(SendMessage), message);

        public async Task<bool> UpdateMessage(Message message)
            => await HubConnection.InvokeAsync<bool>(nameof(UpdateMessage), message);

        public async Task SendPendingMessages()
            => await HubConnection.InvokeAsync(nameof(SendPendingMessages));

        public async Task UpdatePendingMessage(Guid messageid)
            => await HubConnection.InvokeAsync(nameof(UpdatePendingMessage), messageid);
    }
}
