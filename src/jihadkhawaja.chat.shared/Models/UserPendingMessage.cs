using System.ComponentModel.DataAnnotations;

namespace jihadkhawaja.chat.shared.Models
{
    public class UserPendingMessage : EntityCryptographyBase
    {
        [Required]
        public Guid UserId { get; set; }
        [Required]
        public Guid MessageId { get; set; }
        public string? Content { get; set; }
        public DateTimeOffset? DateUserReceivedOn { get; set; }
    }
}
