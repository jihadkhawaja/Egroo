using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : Hub, IChannel
    {
        // Hub's own notification method using SignalR clients.
        private async Task NotifyChannelChange(Guid channelId, params Guid[] extraUserIds)
        {
            // Get all channel users (basic info includes ConnectionId)
            var channelUsers = await _channelRepository.GetChannelUsers(channelId);
            HashSet<Guid> notifiedUserIds = new();

            if (channelUsers != null)
            {
                // Notify all current users in the channel.
                foreach (var user in channelUsers)
                {
                    if (user.ConnectionId != null)
                    {
                        notifiedUserIds.Add(user.Id);
                        await Clients.Client(user.ConnectionId).SendAsync("ChannelChange", channelId);
                    }
                }
            }

            // Additionally notify extra users if provided.
            if (extraUserIds != null)
            {
                foreach (var userId in extraUserIds)
                {
                    if (!notifiedUserIds.Contains(userId))
                    {
                        var userConns = GetUserConnectionIds(userId);
                        await Clients.Clients(userConns).SendAsync("ChannelChange", channelId);
                    }
                }
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<Channel?> CreateChannel(params string[] usernames)
        {
            var channel = await _channelRepository.CreateChannel(usernames);
            if (channel != null)
            {
                await NotifyChannelChange(channel.Id);
            }
            return channel;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> AddChannelUsers(Guid channelId, params string[] usernames)
        {
            bool result = await _channelRepository.AddChannelUsers(channelId, usernames);
            if (result)
            {
                await NotifyChannelChange(channelId);
            }
            return result;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> RemoveChannelUser(Guid channelId, Guid userId)
        {
            bool result = await _channelRepository.RemoveChannelUser(channelId, userId);
            if (result)
            {
                await NotifyChannelChange(channelId, userId);
            }
            return result;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<bool> ChannelContainUser(Guid channelId, Guid userId)
        {
            return _channelRepository.ChannelContainUser(channelId, userId);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<UserDto[]?> GetChannelUsers(Guid channelId)
        {
            return _channelRepository.GetChannelUsers(channelId);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<Channel[]?> GetUserChannels()
        {
            return _channelRepository.GetUserChannels();
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<Channel?> GetChannel(Guid channelId)
        {
            var channel = await _channelRepository.GetChannel(channelId);
            return channel;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<bool> IsChannelAdmin(Guid channelId, Guid userId)
        {
            return _channelRepository.IsChannelAdmin(channelId, userId);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> DeleteChannel(Guid channelId)
        {
            bool result = await _channelRepository.DeleteChannel(channelId);
            if (result)
            {
                await NotifyChannelChange(channelId);
            }
            return result;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> LeaveChannel(Guid channelId)
        {
            bool result = await _channelRepository.LeaveChannel(channelId);
            if (result)
            {
                await NotifyChannelChange(channelId);
            }
            return result;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public Task<Channel[]?> SearchPublicChannels(string searchTerm)
        {
            return _channelRepository.SearchPublicChannels(searchTerm);
        }
    }
}
