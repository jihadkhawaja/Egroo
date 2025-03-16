using System.ComponentModel.DataAnnotations;

namespace jihadkhawaja.chat.shared.Models
{
    public abstract class EntityAudit
    {
        [Required]
        public DateTimeOffset? DateCreated { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DateUpdated { get; set; }
        public DateTimeOffset? DateDeleted { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public Guid? DeletedBy { get; set; }
    }
    public abstract class EntityBase : EntityAudit
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

    }
    public class EntityCryptographyBase : EntityBase
    {
        public bool IsEncrypted { get; set; }
    }
    public abstract class EntityChildBase : EntityAudit
    {
        [Key]
        public int Id { get; set; }
    }
}
