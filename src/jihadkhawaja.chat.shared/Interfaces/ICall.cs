namespace jihadkhawaja.chat.shared.Interfaces
{
    public interface ICall
    {
        /// <summary>
        /// Join an active voice call in a channel. If no call exists, one is created.
        /// The server notifies existing participants so they can establish peer connections.
        /// </summary>
        Task JoinChannelCall(Guid channelId);

        /// <summary>
        /// Leave the current channel voice call.
        /// The server notifies remaining participants to tear down the peer connection.
        /// </summary>
        Task LeaveChannelCall(Guid channelId);

        /// <summary>
        /// Get the list of user IDs currently in a channel's voice call.
        /// </summary>
        Task<Guid[]?> GetChannelCallParticipants(Guid channelId);

        /// <summary>
        /// Send a WebRTC SDP offer to a specific user (by user ID) within a channel call.
        /// </summary>
        Task SendOfferToUser(Guid channelId, Guid targetUserId, string offerSdp);

        /// <summary>
        /// Send a WebRTC SDP answer to a specific user (by user ID) within a channel call.
        /// </summary>
        Task SendAnswerToUser(Guid channelId, Guid targetUserId, string answerSdp);

        /// <summary>
        /// Send an ICE candidate to a specific user (by user ID) within a channel call.
        /// </summary>
        Task SendIceCandidateToUser(Guid channelId, Guid targetUserId, string candidateJson);
    }
}
