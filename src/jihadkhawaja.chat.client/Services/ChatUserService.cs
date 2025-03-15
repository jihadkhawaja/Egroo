using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.chat.client.Services
{
    public class ChatUserService : IUser
    {
        private HubConnection HubConnection => MobileChatSignalR.HubConnection
            ?? throw new NullReferenceException("SignalR not initialized");

        public Task<bool> AddFriend(string friendEmailorusername)
            => HubConnection.InvokeAsync<bool>(nameof(AddFriend), friendEmailorusername);

        public Task<bool> RemoveFriend(string friendEmailorusername)
            => HubConnection.InvokeAsync<bool>(nameof(RemoveFriend), friendEmailorusername);

        public Task CloseUserSession()
            => HubConnection.InvokeAsync(nameof(CloseUserSession));

        public Task<User?> GetUserPublicInfo(Guid userId)
            => HubConnection.InvokeAsync<User?>(nameof(GetUserPublicInfo), userId);

        public Task<string?> GetCurrentUserUsername()
            => HubConnection.InvokeAsync<string?>(nameof(GetCurrentUserUsername));

        public Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
            => HubConnection.InvokeAsync<bool>(nameof(GetUserIsFriend), userId, friendId);

        public Task<UserFriend[]?> GetUserFriends(Guid userId)
            => HubConnection.InvokeAsync<UserFriend[]?>(nameof(GetUserFriends), userId);

        public Task<UserFriend[]?> GetUserFriendRequests(Guid userId)
            => HubConnection.InvokeAsync<UserFriend[]?>(nameof(GetUserFriendRequests), userId);

        public Task<bool> AcceptFriend(Guid friendId)
            => HubConnection.InvokeAsync<bool>(nameof(AcceptFriend), friendId);

        public Task<bool> DenyFriend(Guid friendId)
            => HubConnection.InvokeAsync<bool>(nameof(DenyFriend), friendId);

        public Task<IEnumerable<User>?> SearchUser(string query, int maxResult = 20)
            => HubConnection.InvokeAsync<IEnumerable<User>?>(nameof(SearchUser), query, maxResult);

        public Task<IEnumerable<User>?> SearchUserFriends(string query, int maxResult = 20)
            => HubConnection.InvokeAsync<IEnumerable<User>?>(nameof(SearchUserFriends), query, maxResult);

        public Task<bool> IsUsernameAvailable(string username)
            => HubConnection.InvokeAsync<bool>(nameof(IsUsernameAvailable), username);

        public Task<bool> DeleteUser()
            => HubConnection.InvokeAsync<bool>(nameof(DeleteUser));
    }
}
