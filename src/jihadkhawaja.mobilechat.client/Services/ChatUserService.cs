using jihadkhawaja.mobilechat.client.Interfaces;
using jihadkhawaja.mobilechat.client.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.mobilechat.client.Services
{
    public class ChatUserService : IChatUser
    {
        public async Task<bool> AddFriend(string friendEmailorusername)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<bool>("AddFriend", friendEmailorusername);
        }

        public async Task<bool> RemoveFriend(string friendEmailorusername)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<bool>("RemoveFriend", friendEmailorusername);
        }

        public async Task<string?> GetUserDisplayName(Guid userId)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<string>("GetUserDisplayName", userId);
        }

        public async Task<string?> GetCurrentUserDisplayName()
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<string>("GetCurrentUserDisplayName");
        }

        public async Task<string?> GetUserDisplayNameByEmail(string email)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<string>("GetUserDisplayNameByEmail", email);
        }

        public async Task<string?> GetUserUsername(Guid userId)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<string>("GetUserUsername", userId);
        }

        public async Task<string?> GetCurrentUserUsername()
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<string>("GetCurrentUserUsername");
        }

        public async Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<bool>("GetUserIsFriend", userId, friendId);
        }

        public async Task<UserFriend[]?> GetUserFriends(Guid userId)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<UserFriend[]>("GetUserFriends", userId);
        }

        public async Task<UserFriend[]?> GetUserFriendRequests(Guid userId)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<UserFriend[]>("GetUserFriendRequests", userId);
        }

        public async Task<bool> AcceptFriend(Guid friendId)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<bool>("AcceptFriend", friendId);
        }

        public async Task<bool> DenyFriend(Guid friendId)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<bool>("DenyFriend", friendId);
        }

        public async Task<IEnumerable<User>?> SearchUser(string query, int maxResult = 20)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<IEnumerable<User>?>("SearchUser", query, maxResult);
        }

        public async Task<bool> IsUserOnline(Guid userId)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<bool>("IsUserOnline", userId);
        }
    }
}
