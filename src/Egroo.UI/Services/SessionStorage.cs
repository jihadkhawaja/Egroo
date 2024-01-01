using jihadkhawaja.mobilechat.client.Models;
using Egroo.UI.Models;

namespace Egroo.UI.Services
{
    public class SessionStorage
    {
        public string? Token { get; set; }
        public User? User { get; set; }
        public string? AppDataPath { get; set; }
        public FrameworkPlatform CurrentFrameworkPlatform { get; set; }
    }
}
