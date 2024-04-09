using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    public interface IChatMessage
    {
        Task<bool> SendMessage(Message message);
        Task<bool> SetMessageAsSeen(Guid messageid);
    }
}
