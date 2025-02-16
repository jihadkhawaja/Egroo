using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : IChatCall
    {
        // These dictionaries are declared here (or in your ChatHubCall.cs partial)
        private static readonly ConcurrentDictionary<Guid, UserCall> _userCalls = new();
        private static readonly ConcurrentDictionary<Guid, CallOffer> _callOffers = new();

        #region Call Functionality

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task CallUser(User targetUser)
        {
            var callerId = GetUserIdFromContext();
            if (!callerId.HasValue) return;

            var caller = await _userService.ReadFirst(x => x.Id == callerId.Value);
            var callee = await _userService.ReadFirst(x => x.Id == targetUser.Id);

            if (callee == null || !IsUserOnline(callee.Id))
            {
                // Notify caller only that the target is not available.
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

            if (caller != null && callee != null)
            {
                _callOffers.TryAdd(callee.Id, new CallOffer { Caller = caller, Callee = callee });
                var calleeConns = GetUserConnectionIds(callee.Id);
                await Clients.Clients(calleeConns)
                    .SendAsync("IncomingCall", caller);
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task AnswerCall(bool acceptCall, User caller)
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

            var userCall = new UserCall { Users = new List<User> { callOffer.Caller, callOffer.Callee } };
            _userCalls.TryAdd(callOffer.Caller.Id, userCall);
            _userCalls.TryAdd(callee.Id, userCall);
            _callOffers.TryRemove(callOffer.Callee.Id, out _);

            var callerConnsAccepted = GetUserConnectionIds(callOffer.Caller.Id);
            await Clients.Clients(callerConnsAccepted)
                .SendAsync("CallAccepted", callOffer.Callee);

            await SendUserListUpdate();
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
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

            await SendUserListUpdate();
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task SendSignal(string signal, string targetConnectionId)
        {
            var senderId = GetUserIdFromContext();
            var target = await GetUserFromConnectionId(targetConnectionId);

            if (!senderId.HasValue || target == null) return;

            if (_userCalls.TryGetValue(senderId.Value, out var call) &&
                call.Users.Any(u => u.Id == target.Id))
            {
                var sender = await _userService.ReadFirst(x => x.Id == senderId.Value);
                var targetConns = GetUserConnectionIds(target.Id);
                await Clients.Clients(targetConns)
                    .SendAsync("ReceiveSignal", sender, signal);
            }
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
            // Optionally, you could send this update only to a specific channel group.
            await Clients.All.SendAsync("UpdateUserList", onlineUsers);
        }
        #endregion
    }
}
