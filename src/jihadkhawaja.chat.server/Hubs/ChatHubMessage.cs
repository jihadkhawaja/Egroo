using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace jihadkhawaja.chat.server.Hubs
{
    public partial class ChatHub : IChatMessage
    {
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> SendMessage(Message message)
        {
            if (message == null)
            {
                return false;
            }

            // Validate the connected user is the same as sender
            var connectedUser = await GetConnectedUser();
            if (connectedUser == null || connectedUser.Id != message.SenderId)
            {
                return false;
            }

            // Ensure the user is part of the channel
            if (!await ChannelContainUser(message.ChannelId, message.SenderId))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(message.Content))
            {
                return false;
            }

            // Encrypt message content
            message.Content = _encryptionService.Encrypt(message.Content);

            // Save message to db
            message.Id = Guid.NewGuid();
            message.DateSent = DateTime.UtcNow;
            message.IsEncrypted = true;

            try
            {
                await _dbContext.Messages.AddAsync(message);
                await _dbContext.SaveChangesAsync();

                // Send message to all users in the channel
                User[]? users = await GetChannelUsers(message.ChannelId);
                if (users == null)
                {
                    return false;
                }

                foreach (User user in users)
                {
                    await SendClientMessage(user, message, false);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        private async Task<bool> SendClientMessage(User user, Message message, bool IgnorePendingMessages)
        {
            if (string.IsNullOrWhiteSpace(message.Content))
            {
                return false;
            }

            if (!message.IsEncrypted)
                message.Content = _encryptionService.Encrypt(message.Content);

            message.IsEncrypted = true;

            UserPendingMessage userPendingMessage = new()
            {
                UserId = user.Id,
                MessageId = message.Id,
                Content = message.Content,
                IsEncrypted = message.IsEncrypted
            };

            //In case client was offline or had connection cut
            if (!IgnorePendingMessages)
            {
                try
                {
                    await _dbContext.UsersPendingMessages.AddRangeAsync(userPendingMessage);
                    await _dbContext.SaveChangesAsync();
                }
                catch
                {
                    return false;
                }
            }

            string? connectionId = GetUserConnectionIds(user.Id).LastOrDefault();
            if (string.IsNullOrEmpty(connectionId))
            {
                return false;
            }

            try
            {
                Message messageToSend = message;
                if (message.IsEncrypted)
                {
                    messageToSend.Content = _encryptionService.Decrypt(message.Content);
                    messageToSend.IsEncrypted = false;
                }
                await Clients.Client(connectionId).SendAsync("ReceiveMessage", messageToSend);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send message for the following error: {ex}");
            }

            return false;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> UpdateMessage(Message message)
        {
            if (message.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(message.Content))
            {
                return false;
            }

            Message? dbMessage = await _dbContext.Messages.FirstOrDefaultAsync(x => x.ReferenceId == message.ReferenceId);
            if (dbMessage is null)
            {
                return false;
            }
            dbMessage.DateSeen = DateTimeOffset.UtcNow;
            dbMessage.DateUpdated = DateTimeOffset.UtcNow;

            //save msg to db
            try
            {
                _dbContext.Messages.Update(dbMessage);
                await _dbContext.SaveChangesAsync();

                User[]? users = await GetChannelUsers(dbMessage.ChannelId);
                if (users is null)
                {
                    return false;
                }
                foreach (User user in users)
                {
                    string? connectionId = GetUserConnectionIds(user.Id).LastOrDefault();
                    if (string.IsNullOrEmpty(connectionId))
                    {
                        continue;
                    }

                    await Clients.Client(connectionId).SendAsync("UpdateMessage", dbMessage);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task SendPendingMessages()
        {
            var ConnectedUser = await GetConnectedUser();
            if (ConnectedUser is null)
            {
                return;
            }

            IEnumerable<UserPendingMessage> UserPendingMessages =
                await _dbContext.UsersPendingMessages
                .Where(x => x.UserId == ConnectedUser.Id).ToListAsync();

            if (UserPendingMessages is not null)
            {
                foreach (var userpendingmessage in UserPendingMessages)
                {
                    Message? message = await _dbContext.Messages
                        .FirstOrDefaultAsync(x => x.Id == userpendingmessage.MessageId);

                    if (message is null) continue;

                    message.Content = userpendingmessage.Content;

                    await SendClientMessage(ConnectedUser, message, true);
                }
            }
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task UpdatePendingMessage(Guid messageid)
        {
            var ConnectedUser = await GetConnectedUser();
            if (ConnectedUser is null)
            {
                return;
            }

            UserPendingMessage? UserPendingMessage =
                await _dbContext.UsersPendingMessages
                .FirstOrDefaultAsync(x => x.UserId == ConnectedUser.Id
                    && x.MessageId == messageid
                    && x.DateUserReceivedOn == null
                    && x.DateDeleted == null);

            if (UserPendingMessage is not null)
            {
                try
                {
                    _dbContext.UsersPendingMessages.Remove(UserPendingMessage);
                    await _dbContext.SaveChangesAsync();
                }
                catch
                {
                    return;
                }
            }
        }
    }
}