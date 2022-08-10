using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobileChat.Shared.Models
{
    public class Channel
    {
        [Key]
        public Guid Id { get; set; }
        [NotMapped]
        public string Title { get; set; }
        public DateTime? DateCreated { get; set; }
    }
}
