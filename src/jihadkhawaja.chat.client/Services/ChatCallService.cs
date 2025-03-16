using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.chat.client.Services
{
    public class ChatCallService : ICall
    {
        // Client-side events that UI components can subscribe to.
        // Updated events to include an SDP string where applicable.
        public event Func<UserDto, string, Task>? OnIncomingCall;
        public event Func<UserDto, string, Task>? OnCallAccepted;
        public event Func<UserDto, string, Task>? OnCallDeclined;
        public event Func<UserDto, string, Task>? OnCallEnded;
        public event Func<UserDto, string, Task>? OnReceiveSignal;
        public event Func<List<UserDto>, Task>? OnUpdateUserList;

        private HubConnection HubConnection => MobileChatSignalR.HubConnection
            ?? throw new NullReferenceException("SignalR not initialized");

        public ChatCallService()
        {
            if (MobileChatSignalR.HubConnection is not null)
            {
                // Register hub event handlers.
                MobileChatSignalR.HubConnection.On<UserDto, string>("IncomingCall", async (callingUser, sdpOffer) =>
                {
                    if (OnIncomingCall != null)
                        await OnIncomingCall.Invoke(callingUser, sdpOffer);
                });

                MobileChatSignalR.HubConnection.On<UserDto, string>("CallAccepted", async (acceptingUser, sdpAnswer) =>
                {
                    if (OnCallAccepted != null)
                        await OnCallAccepted.Invoke(acceptingUser, sdpAnswer);
                });

                MobileChatSignalR.HubConnection.On<UserDto, string>("CallDeclined", async (decliningUser, reason) =>
                {
                    if (OnCallDeclined != null)
                        await OnCallDeclined.Invoke(decliningUser, reason);
                });

                MobileChatSignalR.HubConnection.On<UserDto, string>("CallEnded", async (endedUser, message) =>
                {
                    if (OnCallEnded != null)
                        await OnCallEnded.Invoke(endedUser, message);
                });

                MobileChatSignalR.HubConnection.On<UserDto, string>("ReceiveSignal", async (signalingUser, signal) =>
                {
                    if (OnReceiveSignal != null)
                        await OnReceiveSignal.Invoke(signalingUser, signal);
                });

                MobileChatSignalR.HubConnection.On<List<UserDto>>("UpdateUserList", async (userList) =>
                {
                    if (OnUpdateUserList != null)
                        await OnUpdateUserList.Invoke(userList);
                });
            }
        }

        public async Task CallUser(UserDto targetUser, string offerSdp)
            => await HubConnection.InvokeAsync(nameof(CallUser), targetUser, offerSdp);

        public async Task AnswerCall(bool acceptCall, UserDto caller, string answerSdp)
            => await HubConnection.InvokeAsync(nameof(AnswerCall), acceptCall, caller, answerSdp);

        public async Task HangUp()
            => await HubConnection.InvokeAsync(nameof(HangUp));

        public async Task SendSignal(string signal, string targetConnectionId)
            => await HubConnection.InvokeAsync(nameof(SendSignal), signal, targetConnectionId);

        public async Task SendIceCandidateToPeer(string candidateJson)
            => await HubConnection.InvokeAsync("SendIceCandidateToPeer", candidateJson);
    }
}
