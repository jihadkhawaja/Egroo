using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SIPSorcery.Net;  // SIP Sorcery for RTCPeerConnection and SDP handling.
using System.Collections.Concurrent;
using TinyJson;

namespace jihadkhawaja.chat.server.Hubs
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public partial class ChatHub : IChatCall
    {
        // Dictionaries for call state management.
        private static readonly ConcurrentDictionary<Guid, UserCall> _userCalls = new();
        private static readonly ConcurrentDictionary<Guid, CallOffer> _callOffers = new();

        // (In this updated design the RTCPeerConnection objects are no longer created on the server,
        // since each client (browser) now creates its own connection with native WebRTC.)
        // We retain the ICE candidate relay functionality only.
        // private static readonly ConcurrentDictionary<Guid, RTCPeerConnection> _peerConnections = new();

        // ICE server configuration (if needed for any server-side processing).
        private static readonly RTCConfiguration _rtcConfig = new RTCConfiguration
        {
            iceServers = new List<RTCIceServer>
            {
                new RTCIceServer { urls = "stun:stun.l.google.com:19302" }
                // Optionally add TURN servers here.
            }
        };

        #region Call Functionality

        // Caller initiates the call.
        // Updated to accept the SDP offer (which includes audio media) from the caller.
        public async Task CallUser(User targetUser, string sdpOffer)
        {
            if (targetUser == null) return;

            var callerId = GetUserIdFromContext();
            if (!callerId.HasValue) return;

            var caller = await _userService.ReadFirst(x => x.Id == callerId.Value);
            var callee = await _userService.ReadFirst(x => x.Id == targetUser.Id);

            if (callee == null || !IsUserOnline(callee.Id))
            {
                var callerConns = GetUserConnectionIds(callerId.Value);
                await Clients.Clients(callerConns)
                    .SendAsync("CallDeclined", targetUser, "The user is not available.");
                return;
            }

            if (_userCalls.ContainsKey(callee.Id))
            {
                var callerConns = GetUserConnectionIds(callerId.Value);
                await Clients.Clients(callerConns)
                    .SendAsync("CallDeclined", targetUser, $"{callee.Username} is in another call.");
                return;
            }

            // Create and store a call offer with the caller's SDP offer.
            var offer = new CallOffer
            {
                Caller = caller,
                Callee = callee,
                SdpOffer = sdpOffer
            };
            _callOffers.TryAdd(callee.Id, offer);

            // Relay the offer SDP to the callee.
            var calleeConns = GetUserConnectionIds(callee.Id);
            await Clients.Clients(calleeConns)
                .SendAsync("IncomingCall", caller, sdpOffer);
        }

        // Callee answers the call.
        // Updated to accept the SDP answer (which includes its own media) from the callee.
        public async Task AnswerCall(bool acceptCall, User caller, string sdpAnswer)
        {
            var calleeId = GetUserIdFromContext();
            if (!calleeId.HasValue) return;

            var callee = await _userService.ReadFirst(x => x.Id == calleeId.Value);
            var callOffer = _callOffers.Values.FirstOrDefault(o => o.Callee.Id == calleeId.Value);

            if (callOffer == null || callee == null)
            {
                var callerConns = GetUserConnectionIds(caller.Id);
                await Clients.Clients(callerConns)
                    .SendAsync("CallDeclined", caller, "Call offer expired.");
                return;
            }

            if (!acceptCall)
            {
                _callOffers.TryRemove(callOffer.Callee.Id, out _);
                var callerConns = GetUserConnectionIds(callOffer.Caller.Id);
                await Clients.Clients(callerConns)
                    .SendAsync("CallDeclined", callOffer.Caller, $"{callee.Username} declined your call.");
                return;
            }

            // Mark the call as active.
            var userCall = new UserCall { Users = new List<User> { callOffer.Caller, callOffer.Callee } };
            _userCalls.TryAdd(callOffer.Caller.Id, userCall);
            _userCalls.TryAdd(callee.Id, userCall);
            _callOffers.TryRemove(callOffer.Callee.Id, out _);

            // Relay the SDP answer back to the caller.
            var callerConnsAccepted = GetUserConnectionIds(callOffer.Caller.Id);
            await Clients.Clients(callerConnsAccepted)
                .SendAsync("CallAccepted", callOffer.Callee, sdpAnswer);

            await SendUserListUpdate();
        }

        // Ends the call.
        public async Task HangUp()
        {
            var userId = GetUserIdFromContext();
            if (!userId.HasValue) return;

            if (_userCalls.TryRemove(userId.Value, out var call))
            {
                foreach (var otherUser in call.Users.Where(u => u.Id != userId.Value))
                {
                    _userCalls.TryRemove(otherUser.Id, out _);
                    var otherUserConns = GetUserConnectionIds(otherUser.Id);
                    await Clients.Clients(otherUserConns)
                        .SendAsync("CallEnded", otherUser, "The other party ended the call.");
                }
            }

            // In this updated design, the clients handle their own peer connection cleanup.
            await SendUserListUpdate();
        }

        // Used for exchanging ICE candidates and other SDP signaling.
        public async Task SendSignal(string signal, string targetConnectionId)
        {
            var senderId = GetUserIdFromContext();
            var target = await GetUserFromConnectionId(targetConnectionId);

            if (!senderId.HasValue || target == null) return;

            bool activeCall = _userCalls.TryGetValue(senderId.Value, out var call) &&
                              call.Users.Any(u => u.Id == target.Id);
            bool pendingOffer = _callOffers.ContainsKey(target.Id) || _callOffers.ContainsKey(senderId.Value);

            if (activeCall || pendingOffer)
            {
                var sender = await _userService.ReadFirst(x => x.Id == senderId.Value);
                var targetConns = GetUserConnectionIds(target.Id);
                await Clients.Clients(targetConns)
                    .SendAsync("ReceiveSignal", sender, signal);
            }
        }

        public async Task SendIceCandidateToPeer(string candidateJson)
        {
            // Here you can implement logic to send the ICE candidate to the correct peer.
            Console.WriteLine($"Sending ICE Candidate to peer: {candidateJson}");

            // You can send it to a specific client connection
            await Clients.Others.SendAsync("ReceiveIceCandidate", candidateJson);
        }

        private async Task SendUserListUpdate()
        {
            var onlineUsers = new List<User>();
            foreach (var userId in _userConnections.Keys)
            {
                var user = await _userService.ReadFirst(x => x.Id == userId);
                if (user != null)
                {
                    user.IsOnline = true;
                    user.InCall = _userCalls.ContainsKey(userId);
                    onlineUsers.Add(user);
                }
            }
            await Clients.All.SendAsync("UpdateUserList", onlineUsers);
        }
        #endregion
    }
}
