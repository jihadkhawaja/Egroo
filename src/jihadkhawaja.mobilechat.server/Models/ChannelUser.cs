using System.ComponentModel.DataAnnotations;

namespace jihadkhawaja.mobilechat.server.Models
{
    public class ChannelUser : EntityBase
    {
        [Required]
        public Guid ChannelId { get; set; }
        [Required]
        public Guid UserId { get; set; }
        public bool IsAdmin { get; set; }
    }
}
