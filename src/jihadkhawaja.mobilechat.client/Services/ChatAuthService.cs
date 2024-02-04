using jihadkhawaja.mobilechat.client.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.mobilechat.client.Services
{
    public class ChatAuthService : IChatAuth
    {
        public async Task<dynamic?> SignUp(string displayname, string username, string email, string password)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<object?>("SignUp", displayname, username, email, password);
        }

        public async Task<dynamic?> SignIn(string emailorusername, string password)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<object>("SignIn", emailorusername, password);
        }

        public async Task<dynamic?> RefreshSession(string token)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<dynamic?>("RefreshSession", token);
        }

        public async Task<bool> ChangePassword(string emailorusername, string oldpassword, string newpassword)
        {
            if (MobileChatClient.SignalR is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatClient.SignalR.HubConnection.InvokeAsync<bool>("ChangePassword", emailorusername, oldpassword, newpassword);
        }
    }
}
