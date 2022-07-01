using JihadKhawaja.SignalR.Client.Chat.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JihadKhawaja.SignalR.Client.Chat.Interfaces
{
    public interface IChat
    {
        Task<KeyValuePair<Guid, bool>> SignUp(string displayname, string username, string email, string password);
        Task<KeyValuePair<Guid, bool>> SignIn(string emailorusername, string password);
        Task<bool> SendMessage(Message message);
        Task<bool> AddFriend(Guid userId, string friendEmailorusername);
        Task<bool> RemoveFriend(Guid userId, string friendEmailorusername);
        Task<Channel> CreateChannel(Guid userId, params string[] usernames);
        Task<User[]> GetChannelUsers(Guid channelid);
        Task<Channel[]> GetUserChannels(Guid userid);
        Task<Message[]> ReceiveMessageHistory(Guid channelid);
        Task<Message[]> ReceiveMessageHistoryRange(Guid channelid, int index, int range);
        Task<string> GetUserDisplayName(Guid userId);
    }
}
