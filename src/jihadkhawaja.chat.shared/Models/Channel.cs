using System.ComponentModel.DataAnnotations.Schema;

namespace jihadkhawaja.chat.shared.Models
{
    public class Channel : EntityBase
    {
        [NotMapped]
        public string? DefaultTitle { get; set; }
        public string? Title { get; set; }
        public bool IsPublic { get; set; }
    }
}
