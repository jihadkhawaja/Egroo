using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    public interface IChatCall
    {
        public event Func<List<User>, Task>? OnUpdateUserList;
        public event Func<User, Task>? OnCallAccepted;
        public event Func<User, string, Task>? OnCallDeclined;
        public event Func<User, Task>? OnIncomingCall;
        public event Func<User, string, Task>? OnReceiveSignal;
        public event Func<User, string, Task>? OnCallEnded;
        Task UpdateUserList(List<User> userList);
        Task CallAccepted(User acceptingUser);
        Task CallDeclined(User decliningUser, string reason);
        Task IncomingCall(User callingUser);
        Task ReceiveSignal(User signalingUser, string signal);
        Task CallEnded(User signalingUser, string signal);
    }
}
