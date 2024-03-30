/*
 * 
 * Thanks Shhzdmrz for the app demo
 * https://github.com/Shhzdmrz/SignalRCoreWebRTC
 * 
 */

using jihadkhawaja.mobilechat.server.Interfaces;
using jihadkhawaja.mobilechat.server.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace jihadkhawaja.mobilechat.server.Hubs
{
    public partial class ChatHub : IChatCall
    {
        private readonly List<User> _Users = new();
        private readonly List<UserCall> _UserCalls = new();
        private readonly List<CallOffer> _CallOffers = new();
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task Join(string username)
        {
            // Add the new user
            _Users.Add(new User
            {
                Username = username,
                ConnectionId = Context.ConnectionId
            });

            // Send down the new list to all clients
            await SendUserListUpdate();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task CallUser(User targetConnectionId)
        {
            var callingUser = _Users.SingleOrDefault(u => u.ConnectionId == Context.ConnectionId);
            var targetUser = _Users.SingleOrDefault(u => u.ConnectionId == targetConnectionId.ConnectionId);

            // Make sure the person we are trying to call is still here
            if (targetUser == null)
            {
                // If not, let the caller know
                await CallDeclined(targetConnectionId, "The user you called has left.");
                return;
            }

            // And that they aren't already in a call
            if (GetUserCall(targetUser.ConnectionId) != null)
            {
                await CallDeclined(targetConnectionId, string.Format("{0} is already in a call.", targetUser.Username));
                return;
            }

            // They are here, so tell them someone wants to talk
            await IncomingCall(callingUser);

            // Create an offer
            _CallOffers.Add(new CallOffer
            {
                Caller = callingUser,
                Callee = targetUser
            });
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task AnswerCall(bool acceptCall, User targetConnectionId)
        {
            var callingUser = _Users.SingleOrDefault(u => u.ConnectionId == Context.ConnectionId);
            var targetUser = _Users.SingleOrDefault(u => u.ConnectionId == targetConnectionId.ConnectionId);

            // This can only happen if the server-side came down and clients were cleared, while the user
            // still held their browser session.
            if (callingUser == null)
            {
                return;
            }

            // Make sure the original caller has not left the page yet
            if (targetUser == null)
            {
                await CallEnded(targetConnectionId, "The other user in your call has left.");
                return;
            }

            // Send a decline message if the callee said no
            if (acceptCall == false)
            {
                await CallDeclined(callingUser, string.Format("{0} did not accept your call.", callingUser.Username));
                return;
            }

            // Make sure there is still an active offer.  If there isn't, then the other use hung up before the Callee answered.
            var offerCount = _CallOffers.RemoveAll(c => c.Callee.ConnectionId == callingUser.ConnectionId
                                                  && c.Caller.ConnectionId == targetUser.ConnectionId);
            if (offerCount < 1)
            {
                await CallEnded(targetConnectionId, string.Format("{0} has already hung up.", targetUser.Username));
                return;
            }

            // And finally... make sure the user hasn't accepted another call already
            if (GetUserCall(targetUser.ConnectionId) != null)
            {
                // And that they aren't already in a call
                await CallDeclined(targetConnectionId, string.Format("{0} chose to accept someone elses call instead of yours :(", targetUser.Username));
                return;
            }

            // Remove all the other offers for the call initiator, in case they have multiple calls out
            _CallOffers.RemoveAll(c => c.Caller.ConnectionId == targetUser.ConnectionId);

            // Create a new call to match these folks up
            _UserCalls.Add(new UserCall
            {
                Users = new List<User> { callingUser, targetUser }
            });

            // Tell the original caller that the call was accepted
            await CallAccepted(callingUser);

            // Update the user list, since thes two are now in a call
            await SendUserListUpdate();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task HangUp()
        {
            var callingUser = _Users.SingleOrDefault(u => u.ConnectionId == Context.ConnectionId);

            if (callingUser == null)
            {
                return;
            }

            var currentCall = GetUserCall(callingUser.ConnectionId);

            // Send a hang up message to each user in the call, if there is one
            if (currentCall != null)
            {
                foreach (var user in currentCall.Users.Where(u => u.ConnectionId != callingUser.ConnectionId))
                {
                    await CallEnded(callingUser, string.Format("{0} has hung up.", callingUser.Username));
                }

                // Remove the call from the list if there is only one (or none) person left.  This should
                // always trigger now, but will be useful when we implement conferencing.
                currentCall.Users.RemoveAll(u => u.ConnectionId == callingUser.ConnectionId);
                if (currentCall.Users.Count < 2)
                {
                    _UserCalls.Remove(currentCall);
                }
            }

            // Remove all offers initiating from the caller
            _CallOffers.RemoveAll(c => c.Caller.ConnectionId == callingUser.ConnectionId);

            await SendUserListUpdate();
        }

        // WebRTC Signal Handler
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task SendSignal(string signal, string targetConnectionId)
        {
            var callingUser = _Users.SingleOrDefault(u => u.ConnectionId == Context.ConnectionId);
            var targetUser = _Users.SingleOrDefault(u => u.ConnectionId == targetConnectionId);

            // Make sure both users are valid
            if (callingUser == null || targetUser == null)
            {
                return;
            }

            // Make sure that the person sending the signal is in a call
            var userCall = GetUserCall(callingUser.ConnectionId);

            // ...and that the target is the one they are in a call with
            if (userCall != null && userCall.Users.Exists(u => u.ConnectionId == targetUser.ConnectionId))
            {
                // These folks are in a call together, let's let em talk WebRTC
                await ReceiveSignal(callingUser, signal);
            }
        }

        #region Private Helpers

        private async Task SendUserListUpdate()
        {
            _Users.ForEach(u => u.InCall = (GetUserCall(u.ConnectionId) != null));
            await UpdateUserList(_Users);
        }

        private UserCall GetUserCall(string connectionId)
        {
            var matchingCall =
                _UserCalls.SingleOrDefault(uc => uc.Users.SingleOrDefault(u => u.ConnectionId == connectionId) != null);
            return matchingCall;
        }

        public async Task UpdateUserList(List<User> userList)
        {
            await Clients.All.SendAsync("UpdateUserList", userList);
        }

        public async Task CallAccepted(User acceptingUser)
        {
            await Clients.All.SendAsync("CallAccepted", acceptingUser);
        }

        public async Task CallDeclined(User decliningUser, string reason)
        {
            await Clients.All.SendAsync("CallDeclined", reason);
        }

        public async Task IncomingCall(User callingUser)
        {
            await Clients.All.SendAsync("IncomingCall", callingUser);
        }

        public async Task ReceiveSignal(User signalingUser, string signal)
        {
            await Clients.All.SendAsync("ReceiveSignal", signal);
        }

        public Task CallEnded(User signalingUser, string signal)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
