﻿using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.shared.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.chat.client.Services
{
    public class ChatAuthService : IChatAuth
    {
        public async Task<dynamic?> SignUp(string username, string password)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<object?>(nameof(SignUp), username, password);
        }

        public async Task<dynamic?> SignIn(string username, string password)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<object>(nameof(SignIn), username, password);
        }

        public async Task<dynamic?> RefreshSession(string token)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<dynamic?>(nameof(RefreshSession), token);
        }

        public async Task<bool> ChangePassword(string username, string oldpassword, string newpassword)
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            return await MobileChatSignalR.HubConnection.InvokeAsync<bool>(nameof(ChangePassword), username, oldpassword, newpassword);
        }
    }
}
