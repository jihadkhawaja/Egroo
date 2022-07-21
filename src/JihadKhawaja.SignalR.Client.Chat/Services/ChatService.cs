using JihadKhawaja.SignalR.Client.Chat.Interfaces;
using JihadKhawaja.SignalR.Client.Chat.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace JihadKhawaja.SignalR.Client.Chat.Services
{
    public class ChatService : IChat
    {
        public async Task<KeyValuePair<Guid, bool>> SignUp(string displayname, string username, string email, string password)
        {
            return await ClientChat.SignalR.HubConnection.InvokeAsync<KeyValuePair<Guid, bool>>("SignUp", displayname, username, email, password);
        }

        public async Task<KeyValuePair<Guid, bool>> SignIn(string emailorusername, string password)
        {
            return await ClientChat.SignalR.HubConnection.InvokeAsync<KeyValuePair<Guid, bool>>("SignIn", emailorusername, password);
        }

        public async Task<bool> SendMessage(Message message)
        {
            return await ClientChat.SignalR.HubConnection.InvokeAsync<bool>("SendMessage", message);
        }

        public async Task<bool> AddFriend(Guid userId, string friendEmailorusername)
        {
            return await ClientChat.SignalR.HubConnection.InvokeAsync<bool>("AddFriend", userId, friendEmailorusername);
        }

        public async Task<bool> RemoveFriend(Guid userId, string friendEmailorusername)
        {
            return await ClientChat.SignalR.HubConnection.InvokeAsync<bool>("RemoveFriend", userId, friendEmailorusername);
        }

        public async Task<Channel> CreateChannel(Guid userId, params string[] usernames)
        {
            return await ClientChat.SignalR.HubConnection.InvokeAsync<Channel>("CreateChannel", userId, usernames);
        }
        public async Task<User[]> GetChannelUsers(Guid channelid)
        {
            return await ClientChat.SignalR.HubConnection.InvokeAsync<User[]>("GetChannelUsers", channelid);
        }

        public Task<Channel[]> GetUserChannels(Guid userid)
        {
            return ClientChat.SignalR.HubConnection.InvokeAsync<Channel[]>("GetUserChannels", userid);
        }

        public async Task<Message[]> ReceiveMessageHistory(Guid channelid)
        {
            return await ClientChat.SignalR.HubConnection.InvokeAsync<Message[]>("ReceiveMessageHistory", channelid);
        }

        public async Task<Message[]> ReceiveMessageHistoryRange(Guid channelid, int index, int range)
        {
            return await ClientChat.SignalR.HubConnection.InvokeAsync<Message[]>("ReceiveMessageHistoryRange", channelid, index, range);
        }

        public Task<string> GetUserDisplayName(Guid userId)
        {
            return ClientChat.SignalR.HubConnection.InvokeAsync<string>("GetUserDisplayName", userId);
        }
    }
}
