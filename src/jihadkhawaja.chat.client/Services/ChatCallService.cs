﻿using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.chat.client.Services
{
    public class ChatCallService : IChatCall
    {
        // Client-side events that UI components can subscribe to.
        // Updated events to include an SDP string where applicable.
        public event Func<User, string, Task>? OnIncomingCall;
        public event Func<User, string, Task>? OnCallAccepted;
        public event Func<User, string, Task>? OnCallDeclined;
        public event Func<User, string, Task>? OnCallEnded;
        public event Func<User, string, Task>? OnReceiveSignal;
        public event Func<List<User>, Task>? OnUpdateUserList;

        private HubConnection HubConnection => MobileChatSignalR.HubConnection
            ?? throw new NullReferenceException("SignalR not initialized");

        public ChatCallService()
        {
            if (MobileChatSignalR.HubConnection is not null)
            {
                // Register hub event handlers.
                MobileChatSignalR.HubConnection.On<User, string>("IncomingCall", async (callingUser, sdpOffer) =>
                {
                    if (OnIncomingCall != null)
                        await OnIncomingCall.Invoke(callingUser, sdpOffer);
                });

                MobileChatSignalR.HubConnection.On<User, string>("CallAccepted", async (acceptingUser, sdpAnswer) =>
                {
                    if (OnCallAccepted != null)
                        await OnCallAccepted.Invoke(acceptingUser, sdpAnswer);
                });

                MobileChatSignalR.HubConnection.On<User, string>("CallDeclined", async (decliningUser, reason) =>
                {
                    if (OnCallDeclined != null)
                        await OnCallDeclined.Invoke(decliningUser, reason);
                });

                MobileChatSignalR.HubConnection.On<User, string>("CallEnded", async (endedUser, message) =>
                {
                    if (OnCallEnded != null)
                        await OnCallEnded.Invoke(endedUser, message);
                });

                MobileChatSignalR.HubConnection.On<User, string>("ReceiveSignal", async (signalingUser, signal) =>
                {
                    if (OnReceiveSignal != null)
                        await OnReceiveSignal.Invoke(signalingUser, signal);
                });

                MobileChatSignalR.HubConnection.On<List<User>>("UpdateUserList", async (userList) =>
                {
                    if (OnUpdateUserList != null)
                        await OnUpdateUserList.Invoke(userList);
                });
            }
        }

        public Task CallUser(User targetUser, string offerSdp)
            => HubConnection.InvokeAsync(nameof(CallUser), targetUser, offerSdp);

        public Task AnswerCall(bool acceptCall, User caller, string answerSdp)
            => HubConnection.InvokeAsync(nameof(AnswerCall), acceptCall, caller, answerSdp);

        public Task HangUp()
            => HubConnection.InvokeAsync(nameof(HangUp));

        public Task SendSignal(string signal, string targetConnectionId)
            => HubConnection.InvokeAsync(nameof(SendSignal), signal, targetConnectionId);

        public Task SendIceCandidateToPeer(string candidateJson)
            => HubConnection.InvokeAsync("SendIceCandidateToPeer", candidateJson);
    }
}
