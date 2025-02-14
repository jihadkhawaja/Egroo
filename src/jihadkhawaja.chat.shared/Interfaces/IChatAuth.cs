using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    public interface IChatAuth
    {
        Task<Operation.Response> SignUp(string username, string password);
        Task<Operation.Response> SignIn(string username, string password);
        Task<Operation.Response> RefreshSession(string token);
        Task<Operation.Result> ChangePassword(string username, string oldpassword, string newpassword);
    }
}
