using System.ComponentModel.DataAnnotations;

namespace jihadkhawaja.chat.shared.Models
{
    public abstract class EntityBase
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public DateTimeOffset? DateCreated { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DateUpdated { get; set; }
        public DateTimeOffset? DateDeleted { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public Guid? DeletedBy { get; set; }
        public bool IsEncrypted { get; set; }
    }
}
