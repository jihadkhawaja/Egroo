using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.chat.client.Services
{
    public class ChatMessageService : IChatMessage
    {
        public async Task<bool> SendMessage(Message message)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>(nameof(SendMessage), message);
        }

        public async Task<bool> SetMessageAsSeen(Guid messageid)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>(nameof(SetMessageAsSeen), messageid);
        }
    }
}
