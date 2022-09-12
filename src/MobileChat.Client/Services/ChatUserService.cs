using Microsoft.AspNetCore.SignalR.Client;
using MobileChat.Shared.Interfaces;
using MobileChat.Shared.Models;

namespace MobileChat.Client.Services
{
    public class ChatUserService : IChatUser
    {
        public async Task<bool> AddFriend(string friendEmailorusername)
        {
            return await Connection.SignalR.HubConnection.InvokeAsync<bool>("AddFriend", friendEmailorusername);
        }

        public async Task<bool> RemoveFriend(string friendEmailorusername)
        {
            return await Connection.SignalR.HubConnection.InvokeAsync<bool>("RemoveFriend", friendEmailorusername);
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

        public Task<bool> AcceptFriend(Guid friendId)
        {
            return Connection.SignalR.HubConnection.InvokeAsync<bool>("AcceptFriend", friendId);
        }

        public Task<bool> DenyFriend(Guid friendId)
        {
            return Connection.SignalR.HubConnection.InvokeAsync<bool>("DenyFriend", friendId);
        }
    }
}
