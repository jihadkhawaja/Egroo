using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    public interface IChatMessage
    {
        Task<bool> SendMessage(Message message);
        Task<bool> UpdateMessage(Message message);
        Task SendPendingMessages();
        Task UpdatePendingMessage(Guid messageid);
    }
}
