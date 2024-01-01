using jihadkhawaja.mobilechat.client.Interfaces;
using jihadkhawaja.mobilechat.client.Models;
using jihadkhawaja.mobilechat.client.Services;
using System.Text.Json;

namespace Egroo.Server.Test
{
    [TestClass]
    public class MessageTest
    {
        private IChatAuth ChatAuthService { get; set; } = null!;
        private IChatChannel ChatChannelService { get; set; } = null!;
        private IChatMessage ChatMessageService { get; set; } = null!;

        private static Channel Channel { get; set; }
        private static User User { get; set; }

        [TestInitialize]
        public async Task Initialize()
        {
            ChatAuthService = new ChatAuthService();
            ChatChannelService = new ChatChannelService();
            ChatMessageService = new ChatMessageService();

            dynamic? dynamicObj = await ChatAuthService.SignIn("test", "HvrnS4Q4zJ$xaW!3");
            Dictionary<string, object>? result = null;
            if (dynamicObj is not null)
            {
                result = JsonSerializer.Deserialize<Dictionary<string, object>>(dynamicObj);
            }

            //check user
            if (result is not null)
            {
                string Token = result["token"].ToString();

                var cancellationTokenSource = new CancellationTokenSource();
                jihadkhawaja.mobilechat.client.MobileChatClient.Initialize(TestConfig.HubConnectionUrl, Token);
                await jihadkhawaja.mobilechat.client.MobileChatClient.SignalR.Connect(cancellationTokenSource);

                Channel = await ChatChannelService.CreateChannel("test");
            }
            else
            {
                var cancellationTokenSource = new CancellationTokenSource();
                jihadkhawaja.mobilechat.client.MobileChatClient.Initialize(TestConfig.HubConnectionUrl);
                await jihadkhawaja.mobilechat.client.MobileChatClient.SignalR.Connect(cancellationTokenSource);
            }
        }
        [TestMethod]
        public async Task SendMessageTest()
        {
            Message message = new()
            {
                ChannelId = Channel.Id,
                Content = "This is a test.",
                DisplayName = User.DisplayName,
                SenderId = User.Id,
            };

            Assert.IsTrue(await ChatMessageService.SendMessage(message), "Failed to send message.");
        }

        [TestMethod]
        public async Task ReadMessagesTest()
        {
            Message[] messages = await ChatMessageService.ReceiveMessageHistory(Channel.Id);

            Assert.IsNotNull(messages, "Failed to read messages.");
        }

        [TestCleanup]
        public async Task Cleanup()
        {
        }
    }
}
