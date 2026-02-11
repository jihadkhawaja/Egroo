using jihadkhawaja.chat.shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Egroo.Server.Models
{
    [Index(nameof(Username), IsUnique = true)]
    public class User : UserDto
    {
        public string? Password { get; set; }
        public UserSecurity? UserSecuriy { get; set; }
    }

    public class UserSecurity
    {
        public Guid UserId { get; set; }
        public bool IsTwoFactorEnabled { get; set; }
    }
}
