using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.chat.client.Services
{
    public class ChatMessageService : IChatMessage
    {
        private HubConnection HubConnection => MobileChatSignalR.HubConnection
            ?? throw new NullReferenceException("SignalR not initialized");

        public Task<bool> SendMessage(Message message)
            => HubConnection.InvokeAsync<bool>(nameof(SendMessage), message);

        public Task<bool> UpdateMessage(Message message)
            => HubConnection.InvokeAsync<bool>(nameof(UpdateMessage), message);

        public Task SendPendingMessages()
            => HubConnection.InvokeAsync(nameof(SendPendingMessages));

        public Task UpdatePendingMessage(Guid messageid)
            => HubConnection.InvokeAsync(nameof(UpdatePendingMessage), messageid);
    }
}
