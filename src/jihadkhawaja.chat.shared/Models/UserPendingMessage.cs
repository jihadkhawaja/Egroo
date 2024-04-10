using System.ComponentModel.DataAnnotations;

namespace jihadkhawaja.chat.shared.Models
{
    public class UserPendingMessage : EntityBase
    {
        [Required]
        public Guid UserId { get; set; }
        [Required]
        public Guid MessageId { get; set; }
        [Required]
        public string? Content { get; set; }
        public DateTimeOffset? DateUserReceivedOn { get; set; }
    }
}
