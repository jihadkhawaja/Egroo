using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MobileChat.Shared.Interfaces;
using MobileChat.Shared.Models;

namespace MobileChat.Server.Hubs
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
            message.Sent = true;
            message.DateSent = DateTime.UtcNow;

            Message[] messages = new Message[1] { message };
            if (await MessageService.Create(messages))
            {
                foreach (User user in await GetChannelUsers(message.ChannelId))
                {
                    try
                    {
                        await Clients.Client(user.ConnectionId).SendAsync("ReceiveMessage", message);
                    }
                    catch { }
                }

                return true;
            }

            return false;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<bool> UpdateMessage(Message message)
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

            Message[] messages = new Message[1] { message };
            //save msg to db
            if (await MessageService.Update(messages))
            {
                foreach (User user in await GetChannelUsers(message.ChannelId))
                {
                    await Clients.Client(user.ConnectionId).SendAsync("ReceiveMessage", message);
                }

                return true;
            }

            return false;
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<Message[]> ReceiveMessageHistory(Guid channelId)
        {
            HashSet<Message> msgs = (await MessageService.Read(x => x.ChannelId == channelId)).ToHashSet();
            return msgs.ToArray();
        }
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<Message[]> ReceiveMessageHistoryRange(Guid channelId, int index, int range)
        {
            HashSet<Message> msgs = (await ReceiveMessageHistory(channelId)).Skip(index).Take(range).ToHashSet();
            return msgs.ToArray();
        }
    }
}