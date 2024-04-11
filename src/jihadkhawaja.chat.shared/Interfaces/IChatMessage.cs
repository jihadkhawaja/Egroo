using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    public interface IChatMessage
    {
        Task<bool> SendMessage(Message message);
        Task<bool> UpdateMessage(Guid messageid);
        Task SendPendingMessages();
        Task UpdatePendingMessage(Guid messageid);
    }
}
