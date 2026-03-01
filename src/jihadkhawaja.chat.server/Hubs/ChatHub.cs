using jihadkhawaja.chat.shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : Hub
    {
        private readonly IConnectionTracker _connectionTracker;
        private readonly IUser _userRepository;
        private readonly IChannel _channelRepository;
        private readonly IMessageRepository _messageRepository;

        public ChatHub(
            IConnectionTracker connectionTracker,
            IUser userRepository,
            IChannel channelRepository,
            IMessageRepository messageRepository)
        {
            _connectionTracker = connectionTracker;
            _userRepository = userRepository;
            _channelRepository = channelRepository;
            _messageRepository = messageRepository;
        }

        /// <summary>
        /// Extracts the authenticated user ID from the SignalR context.
        /// Relies on the host app's authentication middleware to populate Context.User claims.
        /// </summary>
        private Guid? GetUserIdFromContext()
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out Guid userId))
            {
                return userId;
            }
            return null;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserIdFromContext();
            if (userId.HasValue)
            {
                _connectionTracker.TrackConnection(userId.Value, Context.ConnectionId);

                if (_connectionTracker.GetConnectionCount(userId.Value) == 1)
                {
                    await NotifyFriendsOfStatusChange(userId.Value, isOnline: true);
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserIdFromContext();
            if (userId.HasValue)
            {
                // Clean up any active voice calls for this user
                await RemoveUserFromAllCalls(userId.Value);

                _connectionTracker.UntrackConnection(userId.Value, Context.ConnectionId);

                if (!_connectionTracker.IsUserOnline(userId.Value))
                {
                    await NotifyFriendsOfStatusChange(userId.Value, isOnline: false);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task NotifyFriendsOfStatusChange(Guid changedUserId, bool isOnline)
        {
            var friendRelations = await _userRepository.GetUserFriends(changedUserId);
            if (friendRelations == null) return;

            var friendIds = friendRelations
                .Select(x => x.UserId == changedUserId ? x.FriendUserId : x.UserId)
                .Distinct()
                .ToList();

            foreach (var friendId in friendIds)
            {
                if (_connectionTracker.IsUserOnline(friendId))
                {
                    var connectionIds = _connectionTracker.GetUserConnectionIds(friendId);
                    await Clients.Clients(connectionIds)
                        .SendAsync("FriendStatusChanged", changedUserId, isOnline);
                }
            }
        }
    }
}
