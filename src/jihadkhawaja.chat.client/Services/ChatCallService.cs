using jihadkhawaja.chat.client.Core;
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

        public async Task CallUser(User targetUser, string offerSdp)
        {
            if (MobileChatSignalR.HubConnection is null)
                throw new NullReferenceException("MobileChatClient SignalR not initialized");

            await MobileChatSignalR.HubConnection.InvokeAsync(nameof(CallUser), targetUser, offerSdp);
        }

        public async Task AnswerCall(bool acceptCall, User caller, string answerSdp)
        {
            if (MobileChatSignalR.HubConnection is null)
                throw new NullReferenceException("MobileChatClient SignalR not initialized");

            await MobileChatSignalR.HubConnection.InvokeAsync(nameof(AnswerCall), acceptCall, caller, answerSdp);
        }

        public async Task HangUp()
        {
            if (MobileChatSignalR.HubConnection is null)
                throw new NullReferenceException("MobileChatClient SignalR not initialized");

            await MobileChatSignalR.HubConnection.InvokeAsync(nameof(HangUp));
        }

        public async Task SendSignal(string signal, string targetConnectionId)
        {
            if (MobileChatSignalR.HubConnection is null)
                throw new NullReferenceException("MobileChatClient SignalR not initialized");

            await MobileChatSignalR.HubConnection.InvokeAsync(nameof(SendSignal), signal, targetConnectionId);
        }

        public async Task SendIceCandidateToPeer(string candidateJson)
        {
            if (MobileChatSignalR.HubConnection is null)
                throw new NullReferenceException("MobileChatClient SignalR not initialized");

            // Log or inspect the received candidate JSON
            Console.WriteLine($"Received ICE Candidate: {candidateJson}");

            // Send the ICE candidate to the peer through SignalR.
            await MobileChatSignalR.HubConnection.InvokeAsync("SendIceCandidateToPeer", candidateJson);
        }

    }
}
