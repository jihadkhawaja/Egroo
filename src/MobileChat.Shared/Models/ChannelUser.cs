using System.ComponentModel.DataAnnotations;

namespace MobileChat.Shared.Models
{
    public class ChannelUser
    {
        [Key]
        public Guid Id { get; set; }
        [Required]
        public Guid ChannelId { get; set; }
        [Required]
        public Guid UserId { get; set; }
        public DateTime? DateCreated { get; set; }
    }
}
