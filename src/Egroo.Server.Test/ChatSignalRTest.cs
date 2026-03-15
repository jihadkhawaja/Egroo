using jihadkhawaja.chat.client.Core;

namespace Egroo.Server.Test
{
    [TestClass]
    public class ChatSignalRTest
    {
        [TestMethod]
        public void GetAutomaticReconnectElapsedDelay_TwoRetries_MatchesOverlayThreshold()
        {
            var delay = ChatSignalR.GetAutomaticReconnectElapsedDelay(2);

            Assert.AreEqual(TimeSpan.FromSeconds(2), delay);
        }

        [TestMethod]
        public void GetAutomaticReconnectElapsedDelay_RetryCountBeyondPolicy_UsesAvailableRetries()
        {
            var delay = ChatSignalR.GetAutomaticReconnectElapsedDelay(10);

            Assert.AreEqual(TimeSpan.FromSeconds(42), delay);
        }
    }
}