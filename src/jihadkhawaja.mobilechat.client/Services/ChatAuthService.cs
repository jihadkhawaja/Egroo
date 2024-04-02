using jihadkhawaja.mobilechat.client.Core;
using jihadkhawaja.mobilechat.client.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.mobilechat.client.Services
{
    public class ChatAuthService : IChatAuth
    {
        public async Task<dynamic?> SignUp(string displayname, string username, string password)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<object?>("SignUp", displayname, username, password);
        }

        public async Task<dynamic?> SignIn(string username, string password)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<object>("SignIn", username, password);
        }

        public async Task<dynamic?> RefreshSession(string token)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<dynamic?>("RefreshSession", token);
        }

        public async Task<bool> ChangePassword(string username, string oldpassword, string newpassword)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>("ChangePassword", username, oldpassword, newpassword);
        }
    }
}
