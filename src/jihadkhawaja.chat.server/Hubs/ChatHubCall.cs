using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : IChatCall
    {
        // Events as defined in the interface.
        public event Func<List<User>, Task>? OnUpdateUserList;
        public event Func<User, Task>? OnCallAccepted;
        public event Func<User, string, Task>? OnCallDeclined;
        public event Func<User, Task>? OnIncomingCall;
        public event Func<User, string, Task>? OnReceiveSignal;
        public event Func<User, string, Task>? OnCallEnded;

        // Shared collections (static so all hub instances share the same state)
        private static readonly List<User> _users = new();
        private static readonly List<UserCall> _userCalls = new();
        private static readonly List<CallOffer> _callOffers = new();
        private static readonly object _syncLock = new();

        #region Public Call Methods

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task Join(string username)
        {
            var newUser = new User
            {
                Username = username,
                ConnectionId = Context.ConnectionId
            };

            lock (_syncLock)
            {
                _users.Add(newUser);
            }

            await SendUserListUpdate();
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task CallUser(User targetUserParam)
        {
            User? callingUser, targetUser;
            lock (_syncLock)
            {
                callingUser = _users.SingleOrDefault(u => u.ConnectionId == Context.ConnectionId);
                targetUser = _users.SingleOrDefault(u => u.ConnectionId == targetUserParam.ConnectionId);
            }

            if (targetUser == null)
            {
                await CallDeclined(targetUserParam, "The user you called has left.");
                return;
            }

            if (GetUserCall(targetUser.ConnectionId) != null)
            {
                await CallDeclined(targetUserParam, $"{targetUser.Username} is already in a call.");
                return;
            }

            if (callingUser != null)
            {
                await IncomingCall(callingUser);
                lock (_syncLock)
                {
                    _callOffers.Add(new CallOffer
                    {
                        Caller = callingUser,
                        Callee = targetUser
                    });
                }
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task AnswerCall(bool acceptCall, User targetUserParam)
        {
            User? callee, caller;
            lock (_syncLock)
            {
                callee = _users.SingleOrDefault(u => u.ConnectionId == Context.ConnectionId);
                caller = _users.SingleOrDefault(u => u.ConnectionId == targetUserParam.ConnectionId);
            }

            if (callee == null)
                return;

            if (caller == null)
            {
                await CallEnded(targetUserParam, "The other user in your call has left.");
                return;
            }

            if (!acceptCall)
            {
                await CallDeclined(caller, $"{callee.Username} did not accept your call.");
                return;
            }

            int offersRemoved;
            lock (_syncLock)
            {
                offersRemoved = _callOffers.RemoveAll(o =>
                    o.Callee.ConnectionId == callee.ConnectionId &&
                    o.Caller.ConnectionId == caller.ConnectionId);
            }

            if (offersRemoved < 1)
            {
                await CallEnded(targetUserParam, $"{caller.Username} has already hung up.");
                return;
            }

            if (GetUserCall(caller.ConnectionId) != null)
            {
                await CallDeclined(targetUserParam, $"{caller.Username} chose to accept someone else's call instead of yours.");
                return;
            }

            lock (_syncLock)
            {
                _callOffers.RemoveAll(o => o.Caller.ConnectionId == caller.ConnectionId);
                _userCalls.Add(new UserCall
                {
                    Users = new List<User> { caller, callee }
                });
            }

            await CallAccepted(caller);
            await SendUserListUpdate();
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task HangUp()
        {
            User? callingUser;
            lock (_syncLock)
            {
                callingUser = _users.SingleOrDefault(u => u.ConnectionId == Context.ConnectionId);
            }
            if (callingUser == null)
                return;

            UserCall? currentCall = GetUserCall(callingUser.ConnectionId);
            if (currentCall != null)
            {
                foreach (var user in currentCall.Users.Where(u => u.ConnectionId != callingUser.ConnectionId))
                {
                    await CallEnded(callingUser, $"{callingUser.Username} has hung up.");
                }

                lock (_syncLock)
                {
                    currentCall.Users.RemoveAll(u => u.ConnectionId == callingUser.ConnectionId);
                    if (currentCall.Users.Count < 2)
                    {
                        _userCalls.Remove(currentCall);
                    }
                }
            }

            lock (_syncLock)
            {
                _callOffers.RemoveAll(o => o.Caller.ConnectionId == callingUser.ConnectionId);
            }

            await SendUserListUpdate();
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task SendSignal(string signal, string targetConnectionId)
        {
            User? sender, target;
            lock (_syncLock)
            {
                sender = _users.SingleOrDefault(u => u.ConnectionId == Context.ConnectionId);
                target = _users.SingleOrDefault(u => u.ConnectionId == targetConnectionId);
            }
            if (sender == null || target == null)
                return;

            var call = GetUserCall(sender.ConnectionId);
            if (call != null && call.Users.Any(u => u.ConnectionId == target.ConnectionId))
            {
                await ReceiveSignal(sender, signal);
            }
        }

        #endregion

        #region IChatCall Interface Implementations

        public async Task UpdateUserList(List<User> userList)
        {
            if (OnUpdateUserList != null)
                await OnUpdateUserList.Invoke(userList);
            await Clients.All.SendAsync("UpdateUserList", userList);
        }

        public async Task CallAccepted(User acceptingUser)
        {
            if (OnCallAccepted != null)
                await OnCallAccepted.Invoke(acceptingUser);
            await Clients.All.SendAsync("CallAccepted", acceptingUser);
        }

        public async Task CallDeclined(User decliningUser, string reason)
        {
            if (OnCallDeclined != null)
                await OnCallDeclined.Invoke(decliningUser, reason);
            await Clients.All.SendAsync("CallDeclined", decliningUser, reason);
        }

        public async Task IncomingCall(User callingUser)
        {
            if (OnIncomingCall != null)
                await OnIncomingCall.Invoke(callingUser);
            await Clients.All.SendAsync("IncomingCall", callingUser);
        }

        public async Task ReceiveSignal(User signalingUser, string signal)
        {
            if (OnReceiveSignal != null)
                await OnReceiveSignal.Invoke(signalingUser, signal);
            await Clients.All.SendAsync("ReceiveSignal", signalingUser, signal);
        }

        public async Task CallEnded(User signalingUser, string signal)
        {
            if (OnCallEnded != null)
                await OnCallEnded.Invoke(signalingUser, signal);
            await Clients.All.SendAsync("CallEnded", signalingUser, signal);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Updates each user's InCall status and broadcasts the updated user list.
        /// </summary>
        private async Task SendUserListUpdate()
        {
            List<User> currentUsers;
            lock (_syncLock)
            {
                _users.ForEach(u => u.InCall = (GetUserCall(u.ConnectionId) != null));
                currentUsers = new List<User>(_users);
            }
            await UpdateUserList(currentUsers);
        }

        /// <summary>
        /// Returns the active call (if any) that involves the user with the specified connection ID.
        /// </summary>
        private UserCall? GetUserCall(string connectionId)
        {
            lock (_syncLock)
            {
                return _userCalls.SingleOrDefault(call => call.Users.Any(u => u.ConnectionId == connectionId));
            }
        }

        #endregion
    }
}
