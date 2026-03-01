using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : ICall
    {
        #region Call Functionality

        [Authorize]
        public async Task CallUser(UserDto targetUser, string sdpOffer)
        {
            if (targetUser == null) return;

            var callerId = GetUserIdFromContext();
            if (!callerId.HasValue)
                return;

            var caller = await _userRepository.GetUserPublicDetails(callerId.Value);
            if (caller == null) return;

            if (!_connectionTracker.IsUserOnline(targetUser.Id))
            {
                var callerConns = _connectionTracker.GetUserConnectionIds(callerId.Value);
                await Clients.Clients(callerConns)
                    .SendAsync("CallDeclined", targetUser, "The user is not available.");
                return;
            }

            var calleeConns = _connectionTracker.GetUserConnectionIds(targetUser.Id);
            await Clients.Clients(calleeConns)
                .SendAsync("IncomingCall", caller, sdpOffer);
        }

        [Authorize]
        public async Task AnswerCall(bool acceptCall, UserDto caller, string sdpAnswer)
        {
            var calleeId = GetUserIdFromContext();
            if (!calleeId.HasValue)
                return;

            var callee = await _userRepository.GetUserPublicDetails(calleeId.Value);

            if (!acceptCall)
            {
                var callerConns = _connectionTracker.GetUserConnectionIds(caller.Id);
                await Clients.Clients(callerConns)
                    .SendAsync("CallDeclined", caller, $"{callee?.Username} declined your call.");
                return;
            }

            var callerConnsAccepted = _connectionTracker.GetUserConnectionIds(caller.Id);
            await Clients.Clients(callerConnsAccepted)
                .SendAsync("CallAccepted", callee, sdpAnswer);
        }

        [Authorize]
        public async Task HangUp()
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue)
                return;

            // Notify all connected clients about the hang up
            await Clients.Others.SendAsync("CallEnded", userId.Value, "The other party ended the call.");
        }

        [Authorize]
        public async Task SendSignal(string signal, string targetConnectionId)
        {
            var senderId = GetUserIdFromContext();
            if (!senderId.HasValue)
                return;

            await Clients.Client(targetConnectionId).SendAsync("ReceiveSignal", senderId.Value, signal);
        }

        [Authorize]
        public async Task SendIceCandidateToPeer(string candidateJson)
        {
            var senderId = GetUserIdFromContext();
            if (!senderId.HasValue)
                return;

            // Forward ICE candidates to other connected clients
            await Clients.Others.SendAsync("ReceiveIceCandidate", candidateJson);
        }

        #endregion
    }
}
