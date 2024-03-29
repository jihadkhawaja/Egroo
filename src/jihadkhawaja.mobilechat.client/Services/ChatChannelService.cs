using jihadkhawaja.mobilechat.client.Core;
using jihadkhawaja.mobilechat.client.Interfaces;
using jihadkhawaja.mobilechat.client.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.mobilechat.client.Services
{
    public class ChatChannelService : IChatChannel
    {
        public async Task<Channel?> CreateChannel(params string[] usernames)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<Channel>("CreateChannel", usernames);
        }

        public async Task<bool> AddChannelUsers(Guid channelId, params string[] friendEmailorusername)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>("AddChannelUsers", channelId, friendEmailorusername);
        }

        public async Task<User[]?> GetChannelUsers(Guid channelid)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<User[]>("GetChannelUsers", channelid);
        }

        public async Task<Channel[]?> GetUserChannels()
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<Channel[]>("GetUserChannels");
        }

        public async Task<bool> ChannelContainUser(Guid channelId, Guid userId)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>("ChannelContainUser", channelId, userId);
        }

        public async Task<bool> IsChannelAdmin(Guid channelId, Guid userId)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>("IsChannelAdmin", channelId, userId);
        }

        public async Task<bool> DeleteChannel(Guid channelId)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>("DeleteChannel", channelId);
        }

        public async Task<bool> LeaveChannel(Guid channelId)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>("LeaveChannel", channelId);
        }
    }
}
