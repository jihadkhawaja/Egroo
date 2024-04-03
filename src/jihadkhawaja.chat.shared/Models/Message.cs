using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace jihadkhawaja.chat.shared.Models
{
    public class Message : EntityBase
    {
        [Required]
        public Guid SenderId { get; set; }
        [Required]
        public Guid ChannelId { get; set; }
        public DateTimeOffset? DateSent { get; set; }
        public DateTimeOffset? DateSeen { get; set; }
        [NotMapped]
        public string? DisplayName { get; set; }
        [NotMapped]
        public string? Content { get; set; }
    }
}
