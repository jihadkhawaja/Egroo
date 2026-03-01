using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.chat.client.Services
{
    public class ChatCallService : ICall
    {
        // Channel voice call events
        /// <summary>Fired when receiving the list of existing participants upon joining a call.</summary>
        public event Func<Guid, Guid[], Task>? OnExistingCallParticipants;

        /// <summary>Fired when a new user joins the channel call.</summary>
        public event Func<Guid, Guid, Task>? OnUserJoinedCall;

        /// <summary>Fired when a user leaves the channel call.</summary>
        public event Func<Guid, Guid, Task>? OnUserLeftCall;

        /// <summary>Fired when receiving a WebRTC SDP offer from a peer.</summary>
        public event Func<Guid, Guid, string, Task>? OnReceiveOffer;

        /// <summary>Fired when receiving a WebRTC SDP answer from a peer.</summary>
        public event Func<Guid, Guid, string, Task>? OnReceiveAnswer;

        /// <summary>Fired when receiving an ICE candidate from a peer.</summary>
        public event Func<Guid, Guid, string, Task>? OnReceiveIceCandidate;

        /// <summary>Fired when the call participant list changes (broadcast to all channel members).</summary>
        public event Func<Guid, Guid[], Task>? OnChannelCallParticipantsChanged;

        private HubConnection HubConnection => ChatSignalR.HubConnection
            ?? throw new NullReferenceException("SignalR not initialized");

        public ChatCallService()
        {
            if (ChatSignalR.HubConnection is not null)
            {
                ChatSignalR.HubConnection.On<Guid, Guid[]>("ExistingCallParticipants", async (channelId, participantIds) =>
                {
                    if (OnExistingCallParticipants != null)
                        await OnExistingCallParticipants.Invoke(channelId, participantIds);
                });

                ChatSignalR.HubConnection.On<Guid, Guid>("UserJoinedCall", async (channelId, userId) =>
                {
                    if (OnUserJoinedCall != null)
                        await OnUserJoinedCall.Invoke(channelId, userId);
                });

                ChatSignalR.HubConnection.On<Guid, Guid>("UserLeftCall", async (channelId, userId) =>
                {
                    if (OnUserLeftCall != null)
                        await OnUserLeftCall.Invoke(channelId, userId);
                });

                ChatSignalR.HubConnection.On<Guid, Guid, string>("ReceiveOffer", async (channelId, fromUserId, offerSdp) =>
                {
                    if (OnReceiveOffer != null)
                        await OnReceiveOffer.Invoke(channelId, fromUserId, offerSdp);
                });

                ChatSignalR.HubConnection.On<Guid, Guid, string>("ReceiveAnswer", async (channelId, fromUserId, answerSdp) =>
                {
                    if (OnReceiveAnswer != null)
                        await OnReceiveAnswer.Invoke(channelId, fromUserId, answerSdp);
                });

                ChatSignalR.HubConnection.On<Guid, Guid, string>("ReceiveIceCandidate", async (channelId, fromUserId, candidateJson) =>
                {
                    if (OnReceiveIceCandidate != null)
                        await OnReceiveIceCandidate.Invoke(channelId, fromUserId, candidateJson);
                });

                ChatSignalR.HubConnection.On<Guid, Guid[]>("ChannelCallParticipantsChanged", async (channelId, participantIds) =>
                {
                    if (OnChannelCallParticipantsChanged != null)
                        await OnChannelCallParticipantsChanged.Invoke(channelId, participantIds);
                });
            }
        }

        public async Task JoinChannelCall(Guid channelId)
            => await HubConnection.InvokeAsync(nameof(JoinChannelCall), channelId);

        public async Task LeaveChannelCall(Guid channelId)
            => await HubConnection.InvokeAsync(nameof(LeaveChannelCall), channelId);

        public async Task<Guid[]?> GetChannelCallParticipants(Guid channelId)
            => await HubConnection.InvokeAsync<Guid[]?>(nameof(GetChannelCallParticipants), channelId);

        public async Task SendOfferToUser(Guid channelId, Guid targetUserId, string offerSdp)
            => await HubConnection.InvokeAsync(nameof(SendOfferToUser), channelId, targetUserId, offerSdp);

        public async Task SendAnswerToUser(Guid channelId, Guid targetUserId, string answerSdp)
            => await HubConnection.InvokeAsync(nameof(SendAnswerToUser), channelId, targetUserId, answerSdp);

        public async Task SendIceCandidateToUser(Guid channelId, Guid targetUserId, string candidateJson)
            => await HubConnection.InvokeAsync(nameof(SendIceCandidateToUser), channelId, targetUserId, candidateJson);
    }
}
