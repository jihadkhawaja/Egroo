using System.ComponentModel.DataAnnotations.Schema;

namespace jihadkhawaja.chat.shared.Models
{
    public class Channel : EntityBase
    {
        [NotMapped]
        public string? Title { get; set; }
    }
}
