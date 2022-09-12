using Microsoft.AspNetCore.SignalR.Client;
using MobileChat.Shared.Interfaces;

namespace MobileChat.Client.Services
{
    public class ChatAuthService : IChatAuth
    {
        public async Task<object> SignUp(string displayname, string username, string email, string password)
        {
            return await Connection.SignalR.HubConnection.InvokeAsync<object>("SignUp", displayname, username, email, password);
        }

        public async Task<object> SignIn(string emailorusername, string password)
        {
            return await Connection.SignalR.HubConnection.InvokeAsync<object>("SignIn", emailorusername, password);
        }

        public async Task<bool> ChangePassword(string emailorusername, string oldpassword, string newpassword)
        {
            return await Connection.SignalR.HubConnection.InvokeAsync<bool>("ChangePassword", emailorusername, oldpassword, newpassword);
        }
    }
}
