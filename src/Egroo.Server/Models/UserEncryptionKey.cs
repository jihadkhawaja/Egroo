using System.ComponentModel.DataAnnotations;
using jihadkhawaja.chat.shared.Models;

namespace Egroo.Server.Models
{
    public class UserEncryptionKey : EntityChildBase
    {
        [Required]
        public Guid UserId { get; set; }
        [Required]
        public string PublicKey { get; set; } = string.Empty;
        [Required]
        public string KeyId { get; set; } = string.Empty;
        public string? DeviceLabel { get; set; }
    }
}
