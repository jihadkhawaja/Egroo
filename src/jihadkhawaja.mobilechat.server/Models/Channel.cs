using System.ComponentModel.DataAnnotations.Schema;

namespace jihadkhawaja.mobilechat.server.Models
{
    public class Channel : EntityBase
    {
        [NotMapped]
        public string? Title { get; set; }
    }
}
