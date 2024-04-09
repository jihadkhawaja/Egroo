using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

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

            Message[] messages = [message];
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

            Message[] messages = [dbMessage];
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
    }
}