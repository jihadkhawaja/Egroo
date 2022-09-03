using Microsoft.AspNetCore.SignalR.Client;
using MobileChat.Shared.Interfaces;
using MobileChat.Shared.Models;

namespace MobileChat.Client.Services
{
    public class ChatMessageService : IChatMessage
    {
        public async Task<bool> SendMessage(Message message)
        {
            return await Connection.SignalR.HubConnection.InvokeAsync<bool>("SendMessage", message);
        }

        public async Task<Message[]> ReceiveMessageHistory(Guid channelid)
        {
            return await Connection.SignalR.HubConnection.InvokeAsync<Message[]>("ReceiveMessageHistory", channelid);
        }

        public async Task<Message[]> ReceiveMessageHistoryRange(Guid channelid, int index, int range)
        {
            return await Connection.SignalR.HubConnection.InvokeAsync<Message[]>("ReceiveMessageHistoryRange", channelid, index, range);
        }

        public Task<bool> UpdateMessage(Message message)
        {
            return Connection.SignalR.HubConnection.InvokeAsync<bool>("message", message);
        }
    }
}
