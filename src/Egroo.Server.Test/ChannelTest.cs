using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.client.Services;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace Egroo.Server.Test
{
    [TestClass]
    public class ChannelTest
    {
        private IAuth ChatAuthService { get; set; } = null!;
        private IChatChannel ChatChannelService { get; set; } = null!;
        private static Channel? Channel { get; set; }

        [TestInitialize]
        public async Task Initialize()
        {
            // Create a new HttpClient with a BaseAddress for AuthService only.
            var httpClient = new HttpClient { BaseAddress = new Uri(TestConfig.ApiBaseUrl) };

            ChatAuthService = new AuthService(httpClient);
            ChatChannelService = new ChatChannelService();

            MobileChatSignalR.Initialize(TestConfig.HubConnectionUrl);
            await MobileChatSignalR.HubConnection.StartAsync();

            // Ensure user exists before signing in
            await ChatAuthService.SignUp("test", "HvrnS4Q4zJ$xaW!3");
            var signInResponse = await ChatAuthService.SignIn("test", "HvrnS4Q4zJ$xaW!3");

            Assert.IsNotNull(signInResponse, "Sign-in response is null.");
            Assert.IsTrue(signInResponse.Success, $"Sign-in failed: {signInResponse.Message}");
            Assert.IsNotNull(signInResponse.Token, "Sign-in did not return a token.");

            // Reinitialize SignalR with authentication
            MobileChatSignalR.Initialize(TestConfig.HubConnectionUrl, signInResponse.Token);
            await MobileChatSignalR.HubConnection.StartAsync();

            // Ensure a channel is created
            Channel = await ChatChannelService.CreateChannel("test");
            Assert.IsNotNull(Channel, "Failed to create channel.");
        }

        [TestMethod, Priority(1)]
        public async Task CreateChannelTest()
        {
            // This test is redundant since channel is created in Initialize, but we verify creation
            Assert.IsNotNull(Channel, "CreateChannelTest failed. Channel is null.");
        }

        [TestMethod, Priority(2)]
        public async Task DeleteChannelTest()
        {
            Assert.IsNotNull(Channel, "DeleteChannelTest failed. No channel exists.");

            bool isDeleted = await ChatChannelService.DeleteChannel(Channel.Id);
            Assert.IsTrue(isDeleted, "Failed to delete channel.");
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
