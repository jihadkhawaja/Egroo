using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    public interface IChatCall
    {
        Task CallUser(User targetUser, string offerSdp);
        Task AnswerCall(bool acceptCall, User caller, string answerSdp);
        Task HangUp();
        Task SendSignal(string signal, string targetConnectionId);
        Task SendIceCandidateToPeer(string candidateJson);
    }
}
