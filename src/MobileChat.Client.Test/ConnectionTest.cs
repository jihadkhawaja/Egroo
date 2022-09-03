namespace MobileChat.Client.Test
{
    [TestClass]
    public class ConnectionTest
    {
        private const string hubConnectionURL = "http://localhost:5175/" + "chathub";
        [TestCleanup]
        public void TestClean()
        {
        }
        [TestInitialize]
        public void TestInit()
        {
            Connection.Initialize(hubConnectionURL);
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