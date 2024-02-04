using jihadkhawaja.mobilechat.server.Interfaces;
using jihadkhawaja.mobilechat.server.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace jihadkhawaja.mobilechat.server.Hubs
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

            if (message.SenderId == Guid.Empty)
            {
                return false;
            }

            if (string.IsNullOrEmpty(message.Content) || string.IsNullOrWhiteSpace(message.Content))
            {
                return false;
            }

            //save msg to db
            message.Id = Guid.NewGuid();
            message.DateCreated = DateTime.UtcNow;
            message.DateSent = DateTime.UtcNow;
            message.Sent = true;

            Message[] messages = new Message[1] { message };
            if (await MessageService.Create(messages))
            {
                User[]? users = await GetChannelUsers(message.ChannelId);
                if (users is null)
                {
                    return false;
                }
                foreach (User user in users)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(user.ConnectionId))
                        {
                            continue;
                        }

                        await Clients.Client(user.ConnectionId).SendAsync("ReceiveMessage", message);
                    }
                    catch { }
                }

                return true;
            }

            return false;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> SetMessageAsSeen(Guid messageid)
        {
            if (messageid == Guid.Empty)
            {
                return false;
            }

            Message dbMessage = await MessageService.ReadFirst(x => x.Id == messageid);
            dbMessage.DateSeen = DateTime.UtcNow;
            dbMessage.Seen = true;

            Message[] messages = new Message[1] { dbMessage };
            //save msg to db
            if (await MessageService.Update(messages))
            {
                User[]? users = await GetChannelUsers(dbMessage.ChannelId);
                if (users is null)
                {
                    return false;
                }
                foreach (User user in users)
                {
                    if (string.IsNullOrEmpty(user.ConnectionId))
                    {
                        continue;
                    }

                    await Clients.Client(user.ConnectionId).SendAsync("UpdateMessage", dbMessage);
                }

                return true;
            }

            return false;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<Message[]?> ReceiveMessageHistory(Guid channelId)
        {
            Message[] msgs = (await MessageService.Read(x => x.ChannelId == channelId)).ToArray();
            return msgs;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<Message[]?> ReceiveMessageHistoryRange(Guid channelId, int range)
        {
            Message[]? messages = await ReceiveMessageHistory(channelId);
            if (messages is null)
            {
                return null;
            }
            Message[] msgs = messages.TakeLast(range).ToArray();
            return msgs;
        }
    }
}