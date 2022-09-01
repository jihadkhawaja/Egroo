using Microsoft.AspNetCore.SignalR.Client;
using MobileChat.Shared.Interfaces;
using MobileChat.Shared.Models;

namespace MobileChat.Client.Services
{
    public class ChatService : IChatAuth, IChatUser, IChatChannel, IChatMessage
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
        public async Task<bool> AddChannelUsers(Guid userId, Guid channelId, params string[] friendEmailorusername)
        {
            return await ClientChat.SignalR.HubConnection.InvokeAsync<bool>("AddChannelUsers", userId, channelId, friendEmailorusername);
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

        public Task<string> GetUserUsername(Guid userId)
        {
            return ClientChat.SignalR.HubConnection.InvokeAsync<string>("GetUserUsername", userId);
        }

        public Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
        {
            return ClientChat.SignalR.HubConnection.InvokeAsync<bool>("GetUserIsFriend", userId, friendId);
        }

        public Task<bool> ChangePassword(string emailorusername, string oldpassword, string newpassword)
        {
            throw new NotImplementedException();
        }

        public Task<UserFriend[]> GetUserFriends(Guid userId)
        {
            return ClientChat.SignalR.HubConnection.InvokeAsync<UserFriend[]>("GetUserFriends", userId);
        }

        public Task<UserFriend[]> GetUserFriendRequests(Guid userId)
        {
            return ClientChat.SignalR.HubConnection.InvokeAsync<UserFriend[]>("GetUserFriendRequests", userId);
        }

        public Task<bool> ChannelContainUser(Guid channelId, Guid userId)
        {
            return ClientChat.SignalR.HubConnection.InvokeAsync<bool>("ChannelContainUser", channelId, userId);
        }

        public Task<bool> UpdateMessage(Message message)
        {
            return ClientChat.SignalR.HubConnection.InvokeAsync<bool>("message", message);
        }

        public Task<bool> AcceptFriend(Guid userId, Guid friendId)
        {
            return ClientChat.SignalR.HubConnection.InvokeAsync<bool>("AcceptFriend", userId, friendId);
        }

        public Task<bool> DenyFriend(Guid userId, Guid friendId)
        {
            return ClientChat.SignalR.HubConnection.InvokeAsync<bool>("DenyFriend", userId, friendId);
        }

        public Task<bool> IsChannelAdmin(Guid channelId, Guid userId)
        {
            return ClientChat.SignalR.HubConnection.InvokeAsync<bool>("IsChannelAdmin", channelId, userId);
        }

        public Task<bool> DeleteChannel(Guid channelId, Guid userId)
        {
            return ClientChat.SignalR.HubConnection.InvokeAsync<bool>("DeleteChannel", channelId, userId);
        }

        public Task<bool> LeaveChannel(Guid userId, Guid channelId)
        {
            return ClientChat.SignalR.HubConnection.InvokeAsync<bool>("LeaveChannel", userId, channelId);
        }
    }
}
