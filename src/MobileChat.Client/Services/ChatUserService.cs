using Microsoft.AspNetCore.SignalR.Client;
using MobileChat.Shared.Interfaces;
using MobileChat.Shared.Models;

namespace MobileChat.Client.Services
{
    public class ChatUserService : IChatUser
    {
        public async Task<bool> AddFriend(Guid userId, string friendEmailorusername)
        {
            return await Connection.SignalR.HubConnection.InvokeAsync<bool>("AddFriend", userId, friendEmailorusername);
        }

        public async Task<bool> RemoveFriend(Guid userId, string friendEmailorusername)
        {
            return await Connection.SignalR.HubConnection.InvokeAsync<bool>("RemoveFriend", userId, friendEmailorusername);
        }

        public Task<string> GetUserDisplayName(Guid userId)
        {
            return Connection.SignalR.HubConnection.InvokeAsync<string>("GetUserDisplayName", userId);
        }

        public Task<string> GetUserUsername(Guid userId)
        {
            return Connection.SignalR.HubConnection.InvokeAsync<string>("GetUserUsername", userId);
        }

        public Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
        {
            return Connection.SignalR.HubConnection.InvokeAsync<bool>("GetUserIsFriend", userId, friendId);
        }

        public Task<UserFriend[]> GetUserFriends(Guid userId)
        {
            return Connection.SignalR.HubConnection.InvokeAsync<UserFriend[]>("GetUserFriends", userId);
        }

        public Task<UserFriend[]> GetUserFriendRequests(Guid userId)
        {
            return Connection.SignalR.HubConnection.InvokeAsync<UserFriend[]>("GetUserFriendRequests", userId);
        }

        public Task<bool> AcceptFriend(Guid userId, Guid friendId)
        {
            return Connection.SignalR.HubConnection.InvokeAsync<bool>("AcceptFriend", userId, friendId);
        }

        public Task<bool> DenyFriend(Guid userId, Guid friendId)
        {
            return Connection.SignalR.HubConnection.InvokeAsync<bool>("DenyFriend", userId, friendId);
        }
    }
}
