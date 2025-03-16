using jihadkhawaja.chat.shared.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace jihadkhawaja.chat.server.Models
{
    [Index(nameof(Username), IsUnique = true)]
    public class User : UserDto
    {
        [Required]
        public string? Password { get; set; }
        public UserSecurity? UserSecuriy { get; set; }
    }

    public class UserSecurity
    {
        [Key]
        public Guid UserId { get; set; }
        public bool IsTwoFactorEnabled { get; set; }
    }
}
