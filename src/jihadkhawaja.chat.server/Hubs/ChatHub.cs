using jihadkhawaja.chat.server.Interfaces;
using jihadkhawaja.chat.server.Security;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : Hub
    {
        // Thread-safe dictionary to track active connection IDs per user.
        private static readonly ConcurrentDictionary<Guid, HashSet<string>> _userConnections =
            new ConcurrentDictionary<Guid, HashSet<string>>();

        private readonly IConfiguration _configuration;
        private readonly IEntity<User> _userService;
        private readonly IEntity<UserFriend> _userFriendsService;
        private readonly IEntity<Channel> _channelService;
        private readonly IEntity<ChannelUser> _channelUsersService;
        private readonly IEntity<Message> _messageService;
        private readonly IEntity<UserPendingMessage> _userPendingMessageService;
        private readonly EncryptionService _encryptionService;

        public ChatHub(
            IConfiguration configuration,
            IEntity<User> userService,
            IEntity<UserFriend> userFriendsService,
            IEntity<Channel> channelService,
            IEntity<ChannelUser> channelUsersService,
            IEntity<Message> messageService,
            IEntity<UserPendingMessage> userPendingMessageService)
        {
            _configuration = configuration;
            _userService = userService;
            _userFriendsService = userFriendsService;
            _channelService = channelService;
            _channelUsersService = channelUsersService;
            _messageService = messageService;
            _userPendingMessageService = userPendingMessageService;

            // Initialize the encryption service with configuration values.
            _encryptionService = new EncryptionService(
                keyString: _configuration["Encryption:Key"],
                ivString: _configuration["Encryption:IV"]);
        }

        /// <summary>
        /// Retrieves the user ID from the SignalR connection context.
        /// It first attempts to get the ID from the authenticated Context.User,
        /// then falls back to reading the token from the query string.
        /// </summary>
        /// <returns>A <see cref="Guid"/> representing the user ID if found; otherwise, null.</returns>
        private Guid? GetUserIdFromContext()
        {
            if (Context.User?.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = Context.User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out Guid userId))
                {
                    return userId;
                }
            }
            else
            {
                var httpContext = Context.GetHttpContext();
                string token = httpContext?.Request.Query["access_token"] ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var jwtToken = tokenHandler.ReadJwtToken(token);
                    var claim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                    if (claim != null && Guid.TryParse(claim.Value, out Guid userId))
                    {
                        return userId;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Retrieves the connected User from the database based on the current connection's context.
        /// </summary>
        public async Task<User?> GetConnectedUser()
        {
            var userId = GetUserIdFromContext();
            if (userId.HasValue)
            {
                return await _userService.ReadFirst(x => x.Id == userId.Value);
            }
            return null;
        }

        /// <summary>
        /// Checks if a user is online by verifying if there are any active connection IDs.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>True if the user is online; otherwise, false.</returns>
        public bool IsUserOnline(Guid userId)
        {
            return _userConnections.TryGetValue(userId, out var connections) && connections.Count > 0;
        }

        /// <summary>
        /// Retrieves a User object from the database using the provided connection ID.
        /// It searches through the connection tracking dictionary to find the associated user.
        /// </summary>
        /// <param name="connectionId">The connection ID to search for.</param>
        /// <returns>A <see cref="User"/> if found; otherwise, null.</returns>
        public async Task<User?> GetUserFromConnectionId(string connectionId)
        {
            Guid? foundUserId = null;

            foreach (var kvp in _userConnections)
            {
                lock (kvp.Value)
                {
                    if (kvp.Value.Contains(connectionId))
                    {
                        foundUserId = kvp.Key;
                    }
                }
                if (foundUserId.HasValue)
                {
                    break;
                }
            }

            if (foundUserId.HasValue)
            {
                return await _userService.ReadFirst(x => x.Id == foundUserId.Value);
            }
            return null;
        }

        /// <summary>
        /// Retrieves all active connection IDs for a given user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <returns>A list of connection IDs if found; otherwise, an empty list.</returns>
        public List<string> GetUserConnectionIds(Guid userId)
        {
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                lock (connections)
                {
                    return connections.ToList();
                }
            }
            return new List<string>();
        }

        /// <summary>
        /// Called when a client connects to the hub.
        /// Adds the connection ID to the user's connection list and marks the user as online if it's the first connection.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var userId = GetUserIdFromContext();
            if (userId.HasValue)
            {
                _userConnections.AddOrUpdate(
                    userId.Value,
                    new HashSet<string> { Context.ConnectionId },
                    (key, existingConnections) =>
                    {
                        lock (existingConnections)
                        {
                            existingConnections.Add(Context.ConnectionId);
                        }
                        return existingConnections;
                    });

                if (_userConnections[userId.Value].Count == 1)
                {
                    var user = await _userService.ReadFirst(x => x.Id == userId.Value);
                    if (user != null)
                    {
                        user.IsOnline = true;
                        user.ConnectionId = Context.ConnectionId;
                        await _userService.Update(user);
                    }
                }
            }

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a client disconnects from the hub.
        /// Removes the connection ID from the user's connection list and marks the user as offline if no connections remain.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserIdFromContext();
            if (userId.HasValue)
            {
                if (_userConnections.TryGetValue(userId.Value, out var connections))
                {
                    lock (connections)
                    {
                        connections.Remove(Context.ConnectionId);
                        if (connections.Count == 0)
                        {
                            _userConnections.TryRemove(userId.Value, out _);
                        }
                    }
                }

                if (!IsUserOnline(userId.Value))
                {
                    var user = await _userService.ReadFirst(x => x.Id == userId.Value);
                    if (user != null)
                    {
                        user.IsOnline = false;
                        user.ConnectionId = null;
                        await _userService.Update(user);
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
