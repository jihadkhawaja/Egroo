using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    public interface IChatCall
    {
        Task CallUser(User targetUser);
        Task AnswerCall(bool acceptCall, User caller);
        Task HangUp();
        Task SendSignal(string signal, string targetConnectionId);
    }
}
