using System.Collections.Concurrent;
using jihadkhawaja.chat.shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : ICall
    {
        // Tracks active voice call participants per channel: channelId -> set of userIds
        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, byte>> _activeChannelCalls = new();

        #region Channel Voice Call

        [Authorize]
        public async Task JoinChannelCall(Guid channelId)
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue)
                return;

            // Verify user belongs to the channel
            bool isMember = await _channelRepository.ChannelContainUser(channelId, userId.Value);
            if (!isMember)
                return;

            // Add user to channel call participants
            var participants = _activeChannelCalls.GetOrAdd(channelId, _ => new ConcurrentDictionary<Guid, byte>());
            participants.TryAdd(userId.Value, 0);

            // Get existing participant IDs (excluding the new joiner)
            var existingParticipantIds = participants.Keys.Where(id => id != userId.Value).ToList();

            // Notify existing call participants that a new user joined (for WebRTC signaling)
            foreach (var participantId in existingParticipantIds)
            {
                var participantConns = _connectionTracker.GetUserConnectionIds(participantId);
                if (participantConns.Count > 0)
                {
                    await Clients.Clients(participantConns)
                        .SendAsync("UserJoinedCall", channelId, userId.Value);
                }
            }

            // Send the new joiner the list of existing participants so they can initiate offers
            var callerConns = _connectionTracker.GetUserConnectionIds(userId.Value);
            if (callerConns.Count > 0)
            {
                await Clients.Clients(callerConns)
                    .SendAsync("ExistingCallParticipants", channelId, existingParticipantIds.ToArray());
            }

            // Broadcast updated participant list to ALL channel members (for UI avatars)
            await NotifyChannelCallChanged(channelId);
        }

        [Authorize]
        public async Task LeaveChannelCall(Guid channelId)
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue)
                return;

            await RemoveUserFromChannelCall(channelId, userId.Value);
        }

        [Authorize]
        public Task<Guid[]?> GetChannelCallParticipants(Guid channelId)
        {
            if (_activeChannelCalls.TryGetValue(channelId, out var participants))
            {
                return Task.FromResult<Guid[]?>(participants.Keys.ToArray());
            }
            return Task.FromResult<Guid[]?>(Array.Empty<Guid>());
        }

        [Authorize]
        public async Task SendOfferToUser(Guid channelId, Guid targetUserId, string offerSdp)
        {
            var senderId = GetUserIdFromContext();
            if (!senderId.HasValue)
                return;

            // Verify both users are in the channel call
            if (!IsUserInChannelCall(channelId, senderId.Value) || !IsUserInChannelCall(channelId, targetUserId))
                return;

            var targetConns = _connectionTracker.GetUserConnectionIds(targetUserId);
            if (targetConns.Count > 0)
            {
                await Clients.Clients(targetConns)
                    .SendAsync("ReceiveOffer", channelId, senderId.Value, offerSdp);
            }
        }

        [Authorize]
        public async Task SendAnswerToUser(Guid channelId, Guid targetUserId, string answerSdp)
        {
            var senderId = GetUserIdFromContext();
            if (!senderId.HasValue)
                return;

            if (!IsUserInChannelCall(channelId, senderId.Value) || !IsUserInChannelCall(channelId, targetUserId))
                return;

            var targetConns = _connectionTracker.GetUserConnectionIds(targetUserId);
            if (targetConns.Count > 0)
            {
                await Clients.Clients(targetConns)
                    .SendAsync("ReceiveAnswer", channelId, senderId.Value, answerSdp);
            }
        }

        [Authorize]
        public async Task SendIceCandidateToUser(Guid channelId, Guid targetUserId, string candidateJson)
        {
            var senderId = GetUserIdFromContext();
            if (!senderId.HasValue)
                return;

            if (!IsUserInChannelCall(channelId, senderId.Value) || !IsUserInChannelCall(channelId, targetUserId))
                return;

            var targetConns = _connectionTracker.GetUserConnectionIds(targetUserId);
            if (targetConns.Count > 0)
            {
                await Clients.Clients(targetConns)
                    .SendAsync("ReceiveIceCandidate", channelId, senderId.Value, candidateJson);
            }
        }

        #endregion

        #region Call Helpers

        private bool IsUserInChannelCall(Guid channelId, Guid userId)
        {
            return _activeChannelCalls.TryGetValue(channelId, out var participants)
                && participants.ContainsKey(userId);
        }

        /// <summary>
        /// Broadcasts the current call participant list to ALL online members of the channel,
        /// so non-participants can see who is in the call in real time.
        /// </summary>
        private async Task NotifyChannelCallChanged(Guid channelId)
        {
            var channelUsers = await _channelRepository.GetChannelUsers(channelId);
            if (channelUsers == null) return;

            Guid[] currentParticipants = Array.Empty<Guid>();
            if (_activeChannelCalls.TryGetValue(channelId, out var participants))
            {
                currentParticipants = participants.Keys.ToArray();
            }

            foreach (var user in channelUsers)
            {
                if (_connectionTracker.IsUserOnline(user.Id))
                {
                    var conns = _connectionTracker.GetUserConnectionIds(user.Id);
                    if (conns.Count > 0)
                    {
                        await Clients.Clients(conns)
                            .SendAsync("ChannelCallParticipantsChanged", channelId, currentParticipants);
                    }
                }
            }
        }

        private async Task RemoveUserFromChannelCall(Guid channelId, Guid userId)
        {
            if (!_activeChannelCalls.TryGetValue(channelId, out var participants))
                return;

            if (!participants.TryRemove(userId, out _))
                return;

            // Notify remaining call participants (for WebRTC teardown)
            foreach (var participantId in participants.Keys)
            {
                var participantConns = _connectionTracker.GetUserConnectionIds(participantId);
                if (participantConns.Count > 0)
                {
                    await Clients.Clients(participantConns)
                        .SendAsync("UserLeftCall", channelId, userId);
                }
            }

            // Clean up empty channel calls
            if (participants.IsEmpty)
            {
                _activeChannelCalls.TryRemove(channelId, out _);
            }

            // Broadcast updated participant list to ALL channel members (for UI avatars)
            await NotifyChannelCallChanged(channelId);
        }

        /// <summary>
        /// Called from OnDisconnectedAsync to clean up call state when a user disconnects.
        /// </summary>
        private async Task RemoveUserFromAllCalls(Guid userId)
        {
            var channelIds = _activeChannelCalls
                .Where(kvp => kvp.Value.ContainsKey(userId))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var channelId in channelIds)
            {
                await RemoveUserFromChannelCall(channelId, userId);
            }
        }

        #endregion
    }
}
