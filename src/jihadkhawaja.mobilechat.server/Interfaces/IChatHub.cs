using jihadkhawaja.mobilechat.server.Models;

namespace jihadkhawaja.mobilechat.server.Interfaces
{
    public interface IChatHub
    {
        Task SendAsync(string methodName, object? arg1, object? arg2 = null, object? arg3 = null,
            object? arg4 = null, object? arg5 = null, object? arg6 = null, CancellationToken cancellationToken = default);

        Task UpdateUserList(List<User> userList);
        Task CallAccepted(User acceptingUser);
        Task CallDeclined(User decliningUser, string reason);
        Task IncomingCall(User callingUser);
        Task ReceiveSignal(User signalingUser, string signal);
        Task CallEnded(User signalingUser, string signal);
    }
}
