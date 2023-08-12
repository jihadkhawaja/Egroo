using jihadkhawaja.mobilechat.client.Models;
using MobileChat.UI.Models;

namespace MobileChat.UI.Services
{
    public class SessionStorage
    {
        public User User { get; set; }
        public string AppDataPath { get; set; }
        public FrameworkPlatform CurrentFrameworkPlatform { get; set; }
    }
}
