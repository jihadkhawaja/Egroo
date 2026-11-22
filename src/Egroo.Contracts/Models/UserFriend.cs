using System.ComponentModel.DataAnnotations;

namespace Egroo.Contracts.Models
{
    public class UserFriend : EntityBase
    {
        [Required]
        public Guid UserId { get; set; }
        [Required]
        public Guid FriendUserId { get; set; }
        public DateTimeOffset? DateAcceptedOn { get; set; }
    }
}
