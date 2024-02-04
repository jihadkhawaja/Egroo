using System.ComponentModel.DataAnnotations;

namespace jihadkhawaja.mobilechat.server.Models
{
    public class UserFriend : EntityBase
    {
        [Required]
        public Guid UserId { get; set; }
        [Required]
        public Guid FriendUserId { get; set; }
        public bool IsAccepted { get; set; }
    }
}
