using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.client.Services;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace Egroo.Server.Test
{
    [TestClass]
    public class MessageTest
    {
        private IAuth ChatAuthService { get; set; } = null!;
        private IChannel ChatChannelService { get; set; } = null!;
        private IMessage ChatMessageService { get; set; } = null!;

        private static Channel? Channel { get; set; }
        private static UserDto? User { get; set; }

        [TestInitialize]
        public async Task Initialize()
        {
            // Create a new HttpClient with a BaseAddress for AuthService only.
            var httpClient = new HttpClient { BaseAddress = new Uri(TestConfig.ApiBaseUrl) };
            ChatAuthService = new AuthService(httpClient);
            ChatChannelService = new ChatChannelService();
            ChatMessageService = new ChatMessageService();

            MobileChatSignalR.Initialize(TestConfig.HubConnectionUrl);
            await MobileChatSignalR.HubConnection.StartAsync();

            var signInResponse = await ChatAuthService.SignIn("test", "HvrnS4Q4zJ$xaW!3");
            Assert.IsNotNull(signInResponse, "Sign-in response is null.");
            Assert.IsTrue(signInResponse.Success, $"Sign-in failed: {signInResponse.Message}");
            Assert.IsNotNull(signInResponse.Token, "Sign-in did not return a token.");
            Assert.IsNotNull(signInResponse.UserId, "Sign-in did not return a user ID.");

            // Create User instance
            User = new UserDto
            {
                Id = signInResponse.UserId.Value
            };

            // Reinitialize SignalR with authentication token
            MobileChatSignalR.Initialize(TestConfig.HubConnectionUrl, signInResponse.Token);
            await MobileChatSignalR.HubConnection.StartAsync();

            // Create channel
            Channel = await ChatChannelService.CreateChannel("test");
            Assert.IsNotNull(Channel, "Failed to create channel.");
        }

        [TestMethod, Priority(1)]
        public async Task SendMessageTest()
        {
            Assert.IsNotNull(Channel, "Channel is null. Ensure initialization succeeded.");
            Assert.IsNotNull(User, "User is null. Ensure authentication succeeded.");

            Message message = new()
            {
                ChannelId = Channel.Id,
                Content = "This is a test.",
                SenderId = User.Id,
            };

            bool sendMessageResult = await ChatMessageService.SendMessage(message);
            Assert.IsTrue(sendMessageResult, "Failed to send message.");
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (MobileChatSignalR.HubConnection.State == HubConnectionState.Connected)
            {
                await MobileChatSignalR.HubConnection.StopAsync();
            }
        }
    }
}
