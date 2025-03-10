namespace Egroo.Server.Test
{
    public static class TestConfig
    {
#if DEBUG
        public static string HubConnectionUrl = "http://localhost:5175/chathub";
        public static string ApiBaseUrl = "http://localhost:5175/";
#else
        public static string HubConnectionUrl = "https://dev-api-chat.jihadkhawaja.com/chathub";
        public static string ApiBaseUrl ="https://dev-api-chat.jihadkhawaja.com/";
#endif
    }
}
