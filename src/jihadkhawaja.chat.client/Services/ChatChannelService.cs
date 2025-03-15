using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.chat.client.Services
{
    public class ChatChannelService : IChannel
    {
        private HubConnection HubConnection => MobileChatSignalR.HubConnection
            ?? throw new NullReferenceException("SignalR not initialized");

        public async Task<Channel?> CreateChannel(params string[] usernames)
            => await HubConnection.InvokeAsync<Channel>(nameof(CreateChannel), usernames);

        public async Task<bool> AddChannelUsers(Guid channelId, params string[] friendEmailorusername)
            => await HubConnection.InvokeAsync<bool>(nameof(AddChannelUsers), channelId, friendEmailorusername);

        public async Task<User[]?> GetChannelUsers(Guid channelId)
            => await HubConnection.InvokeAsync<User[]>(nameof(GetChannelUsers), channelId);

        public async Task<Channel?> GetChannel(Guid channelId)
            => await HubConnection.InvokeAsync<Channel>(nameof(GetChannel), channelId);

        public async Task<Channel[]?> GetUserChannels()
            => await HubConnection.InvokeAsync<Channel[]>(nameof(GetUserChannels));

        public async Task<bool> ChannelContainUser(Guid channelId, Guid userId)
            => await HubConnection.InvokeAsync<bool>(nameof(ChannelContainUser), channelId, userId);

        public async Task<bool> IsChannelAdmin(Guid channelId, Guid userId)
            => await HubConnection.InvokeAsync<bool>(nameof(IsChannelAdmin), channelId, userId);

        public async Task<bool> DeleteChannel(Guid channelId)
            => await HubConnection.InvokeAsync<bool>(nameof(DeleteChannel), channelId);

        public async Task<bool> LeaveChannel(Guid channelId)
            => await HubConnection.InvokeAsync<bool>(nameof(LeaveChannel), channelId);

        public async Task<bool> RemoveChannelUser(Guid channelId, Guid userId)
            => await HubConnection.InvokeAsync<bool>(nameof(RemoveChannelUser), channelId, userId);

        public async Task<Channel[]?> SearchPublicChannels(string searchTerm)
            => await HubConnection.InvokeAsync<Channel[]>(nameof(SearchPublicChannels), searchTerm);
    }
}
