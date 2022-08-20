using System.ComponentModel.DataAnnotations;

namespace MobileChat.Shared.Models
{
    public class UserFriend
    {
        [Key]
        public Guid Id { get; set; }
        [Required]
        public Guid UserId { get; set; }
        [Required]
        public Guid FriendUserId { get; set; }
        public bool IsAccepted { get; set; }
        public DateTime? DateCreated { get; set; }
    }
}
