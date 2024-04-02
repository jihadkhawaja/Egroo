using System.ComponentModel.DataAnnotations;

namespace jihadkhawaja.mobilechat.client.Models
{
    public class User : EntityBase
    {
        public string? ConnectionId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? DisplayName { get; set; }
        public string? About { get; set; }
        public string? AvatarBase64 { get; set; }
        [Required]
        public string? Username { get; set; }
        [Required]
        public string? Password { get; set; }
        public bool IsOnline { get; set; }
        public bool InCall { get; set; }
        [Required]
        public string? Role { get; set; }
        public DateTimeOffset? LastLoginDate { get; set; }
    }
}
