using Microsoft.AspNetCore.SignalR.Client;
using MobileChat.Shared.Interfaces;

namespace MobileChat.Client.Services
{
    public class ChatAuthService : IChatAuth
    {
        public async Task<KeyValuePair<Guid, bool>> SignUp(string displayname, string username, string email, string password)
        {
            return await Connection.SignalR.HubConnection.InvokeAsync<KeyValuePair<Guid, bool>>("SignUp", displayname, username, email, password);
        }

        public async Task<KeyValuePair<Guid, bool>> SignIn(string emailorusername, string password)
        {
            return await Connection.SignalR.HubConnection.InvokeAsync<KeyValuePair<Guid, bool>>("SignIn", emailorusername, password);
        }

        public Task<bool> ChangePassword(string emailorusername, string oldpassword, string newpassword)
        {
            throw new NotImplementedException();
        }
    }
}
