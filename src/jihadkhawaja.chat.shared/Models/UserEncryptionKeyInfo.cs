using System.ComponentModel.DataAnnotations;

namespace jihadkhawaja.chat.shared.Models
{
    /// <summary>
    /// Represents a single device encryption key for a user.
    /// Users may have multiple keys (one per device) to support multi-device E2E encryption.
    /// </summary>
    public class UserEncryptionKeyInfo
    {
        [Required]
        public string PublicKey { get; set; } = string.Empty;
        [Required]
        public string KeyId { get; set; } = string.Empty;
        public string? DeviceLabel { get; set; }
        public DateTimeOffset? DateCreated { get; set; }
    }
}
