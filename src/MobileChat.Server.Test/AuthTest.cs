namespace MobileChat.Server.Test
{
    [TestClass]
    public class AuthTest
    {
        public async Task Connect()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            var hubConnectionUrl = "";

            // Act
            jihadkhawaja.mobilechat.client.MobileChat.Initialize(hubConnectionUrl);
            var connected = await jihadkhawaja.mobilechat.client.MobileChat.SignalR.Connect(cancellationTokenSource);

            // Assert
            Assert.IsTrue(connected, "Failed to connect to SignalR hub.");
        }

        [TestMethod]
        public async Task SignIn()
        {
        }

        [TestMethod]
        public async Task SignUp()
        {
        }
    }
}