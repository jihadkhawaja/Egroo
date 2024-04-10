using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.chat.client.Services
{
    public class ChatUserService : IChatUser
    {
        public async Task<bool> AddFriend(string friendEmailorusername)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>(nameof(AddFriend), friendEmailorusername);
        }

        public async Task<bool> RemoveFriend(string friendEmailorusername)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>(nameof(RemoveFriend), friendEmailorusername);
        }

        public async Task<User?> GetUserPublicInfo(Guid userId)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<User>(nameof(GetUserPublicInfo), userId);
        }

        public async Task<string?> GetCurrentUserUsername()
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<string>(nameof(GetCurrentUserUsername));
        }

        public async Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>(nameof(GetUserIsFriend), userId, friendId);
        }

        public async Task<UserFriend[]?> GetUserFriends(Guid userId)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<UserFriend[]>(nameof(GetUserFriends), userId);
        }

        public async Task<UserFriend[]?> GetUserFriendRequests(Guid userId)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<UserFriend[]>(nameof(GetUserFriendRequests), userId);
        }

        public async Task<bool> AcceptFriend(Guid friendId)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>(nameof(AcceptFriend), friendId);
        }

        public async Task<bool> DenyFriend(Guid friendId)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>(nameof(DenyFriend), friendId);
        }

        public async Task<IEnumerable<User>?> SearchUser(string query, int maxResult = 20)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<IEnumerable<User>?>(nameof(SearchUser), query, maxResult);
        }
    }
}
