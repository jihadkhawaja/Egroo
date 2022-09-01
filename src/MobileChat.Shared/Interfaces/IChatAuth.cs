namespace MobileChat.Shared.Interfaces
{
    public interface IChatAuth
    {
        Task<KeyValuePair<Guid, bool>> SignUp(string displayname, string username, string email, string password);
        Task<KeyValuePair<Guid, bool>> SignIn(string emailorusername, string password);
        Task<bool> ChangePassword(string emailorusername, string oldpassword, string newpassword);
    }
}
