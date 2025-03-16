using jihadkhawaja.chat.shared.Models;

namespace Egroo.UI.Services
{
    public class SessionStorage
    {
        public string? Token { get; set; }
        public UserDto? User { get; set; }
        public AppState AppState { get; set; }
    }

    public enum AppState
    {
        INITIATING = 0,
        LOADING_CACHE = 1,
        ESTABLISHING_CONNECTION = 2,
        CREATING_SESSION = 3,
        CONNECTED = 4,
    }
}
