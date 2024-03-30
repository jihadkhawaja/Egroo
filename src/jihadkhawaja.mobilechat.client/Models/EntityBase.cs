using System.ComponentModel.DataAnnotations;

namespace jihadkhawaja.mobilechat.client.Models
{
    public abstract class EntityBase
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public DateTimeOffset? DateCreated { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DateUpdated { get; set; }
        public DateTimeOffset? DateDeleted { get; set; }
    }
}
