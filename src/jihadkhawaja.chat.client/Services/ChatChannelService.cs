using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.chat.client.Services
{
    public class ChatChannelService : IChatChannel
    {
        public async Task<Channel?> CreateChannel(params string[] usernames)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<Channel>(nameof(CreateChannel), usernames);
        }

        public async Task<bool> AddChannelUsers(Guid channelId, params string[] friendEmailorusername)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>(nameof(AddChannelUsers), channelId, friendEmailorusername);
        }

        public async Task<User[]?> GetChannelUsers(Guid channelid)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<User[]>(nameof(GetChannelUsers), channelid);
        }

        public async Task<Channel[]?> GetUserChannels()
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<Channel[]>(nameof(GetUserChannels));
        }

        public async Task<bool> ChannelContainUser(Guid channelId, Guid userId)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>(nameof(ChannelContainUser), channelId, userId);
        }

        public async Task<bool> IsChannelAdmin(Guid channelId, Guid userId)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>(nameof(IsChannelAdmin), channelId, userId);
        }

        public async Task<bool> DeleteChannel(Guid channelId)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>(nameof(DeleteChannel), channelId);
        }

        public async Task<bool> LeaveChannel(Guid channelId)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>(nameof(LeaveChannel), channelId);
        }
    }
}
