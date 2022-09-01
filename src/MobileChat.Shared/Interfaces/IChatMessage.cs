using MobileChat.Shared.Models;

namespace MobileChat.Shared.Interfaces
{
    public interface IChatMessage
    {
        Task<bool> SendMessage(Message message);
        Task<bool> UpdateMessage(Message message);
        Task<Message[]> ReceiveMessageHistory(Guid channelId);
        Task<Message[]> ReceiveMessageHistoryRange(Guid channelId, int index, int range);
    }
}
