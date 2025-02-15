using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace jihadkhawaja.chat.client.Services
{
    public class ChatCallService : IChatCall
    {
        public event Func<List<User>, Task>? OnUpdateUserList;
        public event Func<User, Task>? OnCallAccepted;
        public event Func<User, string, Task>? OnCallDeclined;
        public event Func<User, Task>? OnIncomingCall;
        public event Func<User, string, Task>? OnReceiveSignal;
        public event Func<User, string, Task>? OnCallEnded;

        public ChatCallService()
        {
            RegisterHubHandlers();
        }

        /// <summary>
        /// Registers SignalR hub callback handlers for call events.
        /// </summary>
        private void RegisterHubHandlers()
        {
            if (MobileChatSignalR.HubConnection is null)
            {
                throw new NullReferenceException("MobileChatClient SignalR not initialized");
            }

            MobileChatSignalR.HubConnection.On<List<User>>("UpdateUserList", async (userList) =>
            {
                Console.WriteLine("Received UpdateUserList event with {Count} users", userList.Count);
                if (OnUpdateUserList != null)
                    await OnUpdateUserList.Invoke(userList);
            });

            MobileChatSignalR.HubConnection.On<User>("CallAccepted", async (acceptingUser) =>
            {
                Console.WriteLine("Received CallAccepted event from {User}", acceptingUser.Username);
                if (OnCallAccepted != null)
                    await OnCallAccepted.Invoke(acceptingUser);
            });

            MobileChatSignalR.HubConnection.On<User, string>("CallDeclined", async (decliningUser, reason) =>
            {
                Console.WriteLine("Received CallDeclined event from {User} with reason: {Reason}", decliningUser.Username, reason);
                if (OnCallDeclined != null)
                    await OnCallDeclined.Invoke(decliningUser, reason);
            });

            MobileChatSignalR.HubConnection.On<User>("IncomingCall", async (callingUser) =>
            {
                Console.WriteLine("Received IncomingCall event from {User}", callingUser.Username);
                if (OnIncomingCall != null)
                    await OnIncomingCall.Invoke(callingUser);
            });

            MobileChatSignalR.HubConnection.On<User, string>("ReceiveSignal", async (signalingUser, signal) =>
            {
                Console.WriteLine("Received ReceiveSignal event from {User} with signal: {Signal}", signalingUser.Username, signal);
                if (OnReceiveSignal != null)
                    await OnReceiveSignal.Invoke(signalingUser, signal);
            });

            MobileChatSignalR.HubConnection.On<User, string>("CallEnded", async (signalingUser, signal) =>
            {
                Console.WriteLine("Received CallEnded event from {User} with signal: {Signal}", signalingUser.Username, signal);
                if (OnCallEnded != null)
                    await OnCallEnded.Invoke(signalingUser, signal);
            });
        }

        // IChatCall interface implementations.
        public Task UpdateUserList(List<User> userList)
        {
            return OnUpdateUserList?.Invoke(userList) ?? Task.CompletedTask;
        }

        public Task CallAccepted(User acceptingUser)
        {
            return OnCallAccepted?.Invoke(acceptingUser) ?? Task.CompletedTask;
        }

        public Task CallDeclined(User decliningUser, string reason)
        {
            return OnCallDeclined?.Invoke(decliningUser, reason) ?? Task.CompletedTask;
        }

        public Task IncomingCall(User callingUser)
        {
            return OnIncomingCall?.Invoke(callingUser) ?? Task.CompletedTask;
        }

        public Task ReceiveSignal(User signalingUser, string signal)
        {
            return OnReceiveSignal?.Invoke(signalingUser, signal) ?? Task.CompletedTask;
        }

        public Task CallEnded(User signalingUser, string signal)
        {
            return OnCallEnded?.Invoke(signalingUser, signal) ?? Task.CompletedTask;
        }
    }
}
