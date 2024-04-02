namespace jihadkhawaja.mobilechat.server.Interfaces
{
    public interface IChatAuth
    {
        Task<dynamic?> SignUp(string displayname, string username, string password);
        Task<dynamic?> SignIn(string username, string password);
        Task<dynamic?> RefreshSession(string token);
        Task<bool> ChangePassword(string username, string oldpassword, string newpassword);
    }
}
