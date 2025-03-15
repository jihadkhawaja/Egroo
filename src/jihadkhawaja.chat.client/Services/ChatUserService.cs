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

        public Task<UserDto?> GetUserPublicDetails(Guid userId)
            => HubConnection.InvokeAsync<UserDto?>(nameof(GetUserPublicDetails), userId);

        public Task<UserDto?> GetUserPrivateDetails()
            => HubConnection.InvokeAsync<UserDto?>(nameof(GetUserPrivateDetails));

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

        public Task<IEnumerable<UserDto>?> SearchUser(string query, int maxResult = 20)
            => HubConnection.InvokeAsync<IEnumerable<UserDto>?>(nameof(SearchUser), query, maxResult);

        public Task<IEnumerable<UserDto>?> SearchUserFriends(string query, int maxResult = 20)
            => HubConnection.InvokeAsync<IEnumerable<UserDto>?>(nameof(SearchUserFriends), query, maxResult);

        public Task<bool> IsUsernameAvailable(string username)
            => HubConnection.InvokeAsync<bool>(nameof(IsUsernameAvailable), username);

        public Task<bool> DeleteUser()
            => HubConnection.InvokeAsync<bool>(nameof(DeleteUser));

        public Task<string?> GetAvatar(Guid userId)
            => HubConnection.InvokeAsync<string?>(nameof(GetAvatar), userId);

        public Task<string?> GetCover(Guid userId)
            => HubConnection.InvokeAsync<string?>(nameof(GetCover), userId);

        public Task<bool> UpdateDetails(string? displayname, string? email, string? firstname, string? lastname)
            => HubConnection.InvokeAsync<bool>(nameof(UpdateDetails), displayname, email, firstname, lastname);

        public Task<bool> UpdateAvatar(string? avatarBase64)
            => HubConnection.InvokeAsync<bool>(nameof(UpdateAvatar), avatarBase64);

        public Task<bool> UpdateCover(string? coverBase64)
            => HubConnection.InvokeAsync<bool>(nameof(UpdateCover), coverBase64);
    }
}
