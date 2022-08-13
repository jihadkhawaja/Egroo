namespace MobileChat.Server.Interfaces
{
    public interface IFirebase
    {
        Task<bool> Send(string token, string title, string message);
        Task<bool> SendAll(string title, string message);
    }
}
