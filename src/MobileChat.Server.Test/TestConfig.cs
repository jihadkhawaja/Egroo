namespace MobileChat.Server.Test
{
    public static class TestConfig
    {
#if DEBUG
        public static string HubConnectionUrl = "http://localhost:5175/chathub";
#else
        public static string HubConnectionUrl = "https://dev-api-chat.jihadkhawaja.com/chathub";
#endif
    }
}
