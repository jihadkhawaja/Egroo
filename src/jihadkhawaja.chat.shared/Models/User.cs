using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace jihadkhawaja.chat.shared.Models
{
    [Index(nameof(Username), IsUnique = true)]
    public class User : EntityBase
    {
        [Base64String]
        public string? AvatarBase64 { get; set; }
        [Required]
        public string? Username { get; set; }
        [Required]
        public string? Password { get; set; }
        [NotMapped]
        public string? ConnectionId { get; set; }
        [NotMapped]
        public bool IsOnline { get; set; }
        public bool InCall { get; set; }
        [Required]
        public string? Role { get; set; }
        public DateTimeOffset? LastLoginDate { get; set; }
    }

    public class CallOffer
    {
        public User? Caller { get; set; }
        public User? Callee { get; set; }
        public string SdpOffer { get; set; } = string.Empty;
    }

    public class UserCall
    {
        public List<User>? Users { get; set; }
    }
}
