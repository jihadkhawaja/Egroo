using jihadkhawaja.mobilechat.client.Interfaces;
using jihadkhawaja.mobilechat.client.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.mobilechat.client.Services
{
    public class ChatMessageService : IChatMessage
    {
        public async Task<bool> SendMessage(Message message)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<bool>("SendMessage", message);
        }

        public async Task<Message[]?> ReceiveMessageHistory(Guid channelid)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<Message[]>("ReceiveMessageHistory", channelid);
        }

        public async Task<Message[]?> ReceiveMessageHistoryRange(Guid channelid, int range)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<Message[]>("ReceiveMessageHistoryRange", channelid, range);
        }

        public async Task<bool> SetMessageAsSeen(Guid messageid)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<bool>("SetMessageAsSeen", messageid);
        }
    }
}
