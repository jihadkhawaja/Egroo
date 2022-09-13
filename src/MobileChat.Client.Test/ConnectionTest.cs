namespace MobileChat.Client.Test
{
    [TestClass]
    public class ConnectionTest
    {
        private const string HubConnectionURL = "http://localhost:5175/" + "chathub";
        private const string Token = "";
        [TestCleanup]
        public void TestClean()
        {
        }
        [TestInitialize]
        public void TestInit()
        {
            Connection.Initialize(HubConnectionURL, Token);
        }
        [TestMethod]
        public async Task Connect()
        {
            Assert.IsTrue(await Connection.SignalR.Connect());
        }
        [TestMethod]
        public async Task Disconnect()
        {
            Assert.IsTrue(await Connection.SignalR.Disconnect());
        }
    }
}