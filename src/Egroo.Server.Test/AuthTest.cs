using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.client.Services;
using jihadkhawaja.chat.shared.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;

namespace Egroo.Server.Test
{
    [TestClass]
    public class AuthTest
    {
        private IChatAuth ChatAuthService { get; set; } = null!;

        [TestInitialize]
        public async Task Initialize()
        {
            ChatAuthService = new ChatAuthService();

            MobileChatSignalR.Initialize(TestConfig.HubConnectionUrl);
            await MobileChatSignalR.HubConnection.StartAsync();
        }

        [TestMethod, Priority(0)]
        public async Task ConnectTest()
        {
            Assert.IsNotNull(MobileChatSignalR.HubConnection, "SignalR connection is null.");
            Assert.AreEqual(HubConnectionState.Connected, MobileChatSignalR.HubConnection.State, "Failed to connect to SignalR hub.");
        }

        [TestMethod, Priority(1)]
        public async Task SignUpThenSignInTest()
        {
            // Try signing up
            var signUpResponse = await ChatAuthService.SignUp("test", "HvrnS4Q4zJ$xaW!3");

            // Check if the sign-up failed for a reason OTHER than "username exists"
            if (!signUpResponse.Success && !(signUpResponse.Message?.ToLower().Contains("exist") ?? false))
            {
                Assert.Fail($"Sign-up failed: {signUpResponse.Message}");
            }

            // Now sign in
            var signInResponse = await ChatAuthService.SignIn("test", "HvrnS4Q4zJ$xaW!3");

            Assert.IsNotNull(signInResponse, "Sign-in response is null.");
            Assert.IsTrue(signInResponse.Success, $"Sign-in failed: {signInResponse.Message}");
            Assert.IsNotNull(signInResponse.Token, "Sign-in did not return a token.");
            Assert.IsNotNull(signInResponse.UserId, "Sign-in did not return a user ID.");
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
