using jihadkhawaja.mobilechat.client.Models;

namespace jihadkhawaja.mobilechat.client.Interfaces
{
    public interface IChatMessage
    {
        Task<bool> SendMessage(Message message);
        Task<bool> SetMessageAsSeen(Guid messageid);
        Task<Message[]?> ReceiveMessageHistory(Guid channelId);
        Task<Message[]?> ReceiveMessageHistoryRange(Guid channelId, int range);
    }
}
