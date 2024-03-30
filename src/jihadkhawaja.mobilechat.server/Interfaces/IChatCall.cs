using jihadkhawaja.mobilechat.server.Models;

namespace jihadkhawaja.mobilechat.server.Interfaces
{
    public interface IChatCall
    {
        Task UpdateUserList(List<User> userList);
        Task CallAccepted(User acceptingUser);
        Task CallDeclined(User decliningUser, string reason);
        Task IncomingCall(User callingUser);
        Task ReceiveSignal(User signalingUser, string signal);
        Task CallEnded(User signalingUser, string signal);
    }
}
