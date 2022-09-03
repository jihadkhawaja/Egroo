using Microsoft.AspNetCore.SignalR.Client;
using MobileChat.Shared.Interfaces;
using MobileChat.Shared.Models;

namespace MobileChat.Client.Services
{
    public class ChatChannelService : IChatChannel
    {
        public async Task<Channel> CreateChannel(Guid userId, params string[] usernames)
        {
            return await Connection.SignalR.HubConnection.InvokeAsync<Channel>("CreateChannel", userId, usernames);
        }

        public async Task<bool> AddChannelUsers(Guid userId, Guid channelId, params string[] friendEmailorusername)
        {
            return await Connection.SignalR.HubConnection.InvokeAsync<bool>("AddChannelUsers", userId, channelId, friendEmailorusername);
        }

        public async Task<User[]> GetChannelUsers(Guid channelid)
        {
            return await Connection.SignalR.HubConnection.InvokeAsync<User[]>("GetChannelUsers", channelid);
        }

        public Task<Channel[]> GetUserChannels(Guid userid)
        {
            return Connection.SignalR.HubConnection.InvokeAsync<Channel[]>("GetUserChannels", userid);
        }

        public Task<bool> ChannelContainUser(Guid channelId, Guid userId)
        {
            return Connection.SignalR.HubConnection.InvokeAsync<bool>("ChannelContainUser", channelId, userId);
        }

        public Task<bool> IsChannelAdmin(Guid channelId, Guid userId)
        {
            return Connection.SignalR.HubConnection.InvokeAsync<bool>("IsChannelAdmin", channelId, userId);
        }

        public Task<bool> DeleteChannel(Guid channelId, Guid userId)
        {
            return Connection.SignalR.HubConnection.InvokeAsync<bool>("DeleteChannel", channelId, userId);
        }

        public Task<bool> LeaveChannel(Guid userId, Guid channelId)
        {
            return Connection.SignalR.HubConnection.InvokeAsync<bool>("LeaveChannel", userId, channelId);
        }
    }
}
