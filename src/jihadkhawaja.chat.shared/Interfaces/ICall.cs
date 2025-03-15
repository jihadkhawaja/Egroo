using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    public interface ICall
    {
        Task CallUser(UserDto targetUser, string offerSdp);
        Task AnswerCall(bool acceptCall, UserDto caller, string answerSdp);
        Task HangUp();
        Task SendSignal(string signal, string targetConnectionId);
        Task SendIceCandidateToPeer(string candidateJson);
    }
}
