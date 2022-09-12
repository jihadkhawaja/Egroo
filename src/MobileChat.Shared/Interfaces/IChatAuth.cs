namespace MobileChat.Shared.Interfaces
{
    public interface IChatAuth
    {
        Task<dynamic> SignUp(string displayname, string username, string email, string password);
        Task<dynamic> SignIn(string emailorusername, string password);
        Task<bool> ChangePassword(string emailorusername, string oldpassword, string newpassword);
    }
}
