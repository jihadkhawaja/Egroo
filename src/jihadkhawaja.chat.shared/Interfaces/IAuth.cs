using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    public interface IAuth
    {
        Task<Operation.Response> SignUp(string username, string password);
        Task<Operation.Response> SignIn(string username, string password);
        Task<Operation.Response> RefreshSession();
        Task<Operation.Result> ChangePassword(string oldpassword, string newpassword);
    }
    public interface IAuthClient : IAuth
    {
        HttpClient HttpClient { get; }
    }
}
