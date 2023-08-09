using jihadkhawaja.mobilechat.client.Interfaces;
using jihadkhawaja.mobilechat.client.Services;
using System.Text.Json;

namespace MobileChat.Server.Test
{
    [TestClass]
    public class AuthTest
    {
        private IChatAuth ChatAuthService { get; set; } = null!;

        [TestInitialize]
        public async Task Initialize()
        {
            ChatAuthService = new ChatAuthService();

            var cancellationTokenSource = new CancellationTokenSource();
            jihadkhawaja.mobilechat.client.MobileChat.Initialize(TestConfig.HubConnectionUrl);
            await jihadkhawaja.mobilechat.client.MobileChat.SignalR.Connect(cancellationTokenSource);
        }
        [TestMethod]
        public async Task ConnectTest()
        {
            if (jihadkhawaja.mobilechat.client.MobileChat.SignalR.HubConnection.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
            {
                Assert.IsTrue(true);
            }
            else
            {
                Assert.IsTrue(false, "Failed to connect to SignalR hub.");
            }
        }

        [TestMethod]
        public async Task SignUpThenSignInTest()
        {
            dynamic? dynamicObj2 = await ChatAuthService.SignUp("Test", "test", "test@domain.com", "HvrnS4Q4zJ$xaW!3");
            Dictionary<string, object>? result2 = null;
            if (dynamicObj2 is not null)
            {
                result2 = JsonSerializer.Deserialize<Dictionary<string, object>>(dynamicObj2);
            }

            Assert.IsNotNull(result2, "Failed to sign up or user already exist.");

            dynamic? dynamicObj = await ChatAuthService.SignIn("test", "HvrnS4Q4zJ$xaW!3");
            Dictionary<string, object>? result = null;
            if (dynamicObj is not null)
            {
                result = JsonSerializer.Deserialize<Dictionary<string, object>>(dynamicObj);
            }

            if (result is not null)
            {
                Assert.IsNotNull(result, "Failed to sign in when user exist.");
                return;
            }
        }

        [TestCleanup]
        public async Task Cleanup()
        {
        }
    }
}