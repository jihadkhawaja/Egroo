using jihadkhawaja.mobilechat.server.Models;

namespace jihadkhawaja.mobilechat.server.Interfaces
{
    public interface IChatMessage
    {
        Task<bool> SendMessage(Message message);
        Task<bool> SetMessageAsSeen(Guid messageid);
        Task<Message[]?> ReceiveMessageHistory(Guid channelId);
        Task<Message[]?> ReceiveMessageHistoryRange(Guid channelId, int range);
    }
}
