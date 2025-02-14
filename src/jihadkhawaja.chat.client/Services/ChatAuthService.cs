using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.chat.client.Services
{
    public class ChatAuthService : IChatAuth
    {
        public async Task<Operation.Response> SignUp(string username, string password)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<Operation.Response>(nameof(SignUp), username, password);
        }

        public async Task<Operation.Response> SignIn(string username, string password)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<Operation.Response>(nameof(SignIn), username, password);
        }

        public async Task<Operation.Response> RefreshSession(string token)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<Operation.Response>(nameof(RefreshSession), token);
        }

        public async Task<Operation.Result> ChangePassword(string username, string oldpassword, string newpassword)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<Operation.Result>(nameof(ChangePassword), username, oldpassword, newpassword);
        }
    }
}
