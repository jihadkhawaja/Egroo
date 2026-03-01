using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : Hub, IChannel
    {
        private async Task NotifyChannelChange(Guid channelId, params Guid[] extraUserIds)
        {
            var channelUsers = await _channelRepository.GetChannelUsers(channelId);
            HashSet<Guid> notifiedUserIds = new();

            if (channelUsers != null)
            {
                foreach (var user in channelUsers)
                {
                    notifiedUserIds.Add(user.Id);
                    var userConns = _connectionTracker.GetUserConnectionIds(user.Id);
                    if (userConns.Count > 0)
                    {
                        await Clients.Clients(userConns).SendAsync("ChannelChange", channelId);
                    }
                }
            }

            if (extraUserIds != null)
            {
                foreach (var userId in extraUserIds)
                {
                    if (!notifiedUserIds.Contains(userId))
                    {
                        var userConns = _connectionTracker.GetUserConnectionIds(userId);
                        await Clients.Clients(userConns).SendAsync("ChannelChange", channelId);
                    }
                }
            }
        }

        [Authorize]
        public async Task<Channel?> CreateChannel(params string[] usernames)
        {
            var channel = await _channelRepository.CreateChannel(usernames);
            if (channel != null)
            {
                await NotifyChannelChange(channel.Id);
            }
            return channel;
        }

        [Authorize]
        public async Task<bool> AddChannelUsers(Guid channelId, params string[] usernames)
        {
            bool result = await _channelRepository.AddChannelUsers(channelId, usernames);
            if (result)
            {
                await NotifyChannelChange(channelId);
            }
            return result;
        }

        [Authorize]
        public async Task<bool> RemoveChannelUser(Guid channelId, Guid userId)
        {
            bool result = await _channelRepository.RemoveChannelUser(channelId, userId);
            if (result)
            {
                await NotifyChannelChange(channelId, userId);
            }
            return result;
        }

        [Authorize]
        public Task<bool> ChannelContainUser(Guid channelId, Guid userId)
        {
            return _channelRepository.ChannelContainUser(channelId, userId);
        }

        [Authorize]
        public Task<UserDto[]?> GetChannelUsers(Guid channelId)
        {
            return _channelRepository.GetChannelUsers(channelId);
        }

        [Authorize]
        public Task<Channel[]?> GetUserChannels()
        {
            return _channelRepository.GetUserChannels();
        }

        [Authorize]
        public async Task<Channel?> GetChannel(Guid channelId)
        {
            return await _channelRepository.GetChannel(channelId);
        }

        [Authorize]
        public Task<bool> IsChannelAdmin(Guid channelId, Guid userId)
        {
            return _channelRepository.IsChannelAdmin(channelId, userId);
        }

        [Authorize]
        public async Task<bool> DeleteChannel(Guid channelId)
        {
            bool result = await _channelRepository.DeleteChannel(channelId);
            if (result)
            {
                await NotifyChannelChange(channelId);
            }
            return result;
        }

        [Authorize]
        public async Task<bool> LeaveChannel(Guid channelId)
        {
            bool result = await _channelRepository.LeaveChannel(channelId);
            if (result)
            {
                await NotifyChannelChange(channelId);
            }
            return result;
        }

        [Authorize]
        public Task<Channel[]?> SearchPublicChannels(string searchTerm)
        {
            return _channelRepository.SearchPublicChannels(searchTerm);
        }
    }
}
