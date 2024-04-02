using System.ComponentModel.DataAnnotations;

namespace jihadkhawaja.mobilechat.server.Models
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

    public class CallOffer
    {
        public User Caller { get; set; }
        public User Callee { get; set; }
    }

    public class UserCall
    {
        public List<User> Users { get; set; }
    }
}
