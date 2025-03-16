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

        public async Task<bool> AddFriend(string friendEmailorusername)
            => await HubConnection.InvokeAsync<bool>(nameof(AddFriend), friendEmailorusername);

        public async Task<bool> RemoveFriend(string friendEmailorusername)
            => await HubConnection.InvokeAsync<bool>(nameof(RemoveFriend), friendEmailorusername);

        public async Task CloseUserSession()
            => await HubConnection.InvokeAsync(nameof(CloseUserSession));

        public async Task<UserDto?> GetUserPublicDetails(Guid userId)
            => await HubConnection.InvokeAsync<UserDto?>(nameof(GetUserPublicDetails), userId);

        public async Task<UserDto?> GetUserPrivateDetails()
            => await HubConnection.InvokeAsync<UserDto?>(nameof(GetUserPrivateDetails));

        public async Task<string?> GetCurrentUserUsername()
            => await HubConnection.InvokeAsync<string?>(nameof(GetCurrentUserUsername));

        public async Task<bool> GetUserIsFriend(Guid userId, Guid friendId)
            => await HubConnection.InvokeAsync<bool>(nameof(GetUserIsFriend), userId, friendId);

        public async Task<UserFriend[]?> GetUserFriends(Guid userId)
            => await HubConnection.InvokeAsync<UserFriend[]?>(nameof(GetUserFriends), userId);

        public async Task<UserFriend[]?> GetUserFriendRequests(Guid userId)
            => await HubConnection.InvokeAsync<UserFriend[]?>(nameof(GetUserFriendRequests), userId);

        public async Task<bool> AcceptFriend(Guid friendId)
            => await HubConnection.InvokeAsync<bool>(nameof(AcceptFriend), friendId);

        public async Task<bool> DenyFriend(Guid friendId)
            => await HubConnection.InvokeAsync<bool>(nameof(DenyFriend), friendId);

        public async Task<IEnumerable<UserDto>?> SearchUser(string query, int maxResult = 20)
            => await HubConnection.InvokeAsync<IEnumerable<UserDto>?>(nameof(SearchUser), query, maxResult);

        public async Task<IEnumerable<UserDto>?> SearchUserFriends(string query, int maxResult = 20)
            => await HubConnection.InvokeAsync<IEnumerable<UserDto>?>(nameof(SearchUserFriends), query, maxResult);

        public async Task<bool> IsUsernameAvailable(string username)
            => await HubConnection.InvokeAsync<bool>(nameof(IsUsernameAvailable), username);

        public async Task<bool> DeleteUser()
            => await HubConnection.InvokeAsync<bool>(nameof(DeleteUser));

        public async Task<MediaResult?> GetAvatar(Guid userId)
            => await HubConnection.InvokeAsync<MediaResult?>(nameof(GetAvatar), userId);

        public async Task<MediaResult?> GetCover(Guid userId)
            => await HubConnection.InvokeAsync<MediaResult?>(nameof(GetCover), userId);

        public async Task<bool> UpdateDetails(string? displayname, string? email, string? firstname, string? lastname)
            => await HubConnection.InvokeAsync<bool>(nameof(UpdateDetails), displayname, email, firstname, lastname);

        public async Task<bool> UpdateAvatar(string? avatarBase64)
            => await HubConnection.InvokeAsync<bool>(nameof(UpdateAvatar), avatarBase64);

        public async Task<bool> UpdateCover(string? coverBase64)
            => await HubConnection.InvokeAsync<bool>(nameof(UpdateCover), coverBase64);

        public async Task<bool> SendFeedback(string text)
            => await HubConnection.InvokeAsync<bool>(nameof(SendFeedback), text);
    }
}
