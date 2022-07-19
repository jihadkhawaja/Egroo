using System.ComponentModel.DataAnnotations;

namespace JihadKhawaja.SignalR.Client.Chat.Models
{
    public class Channel
    {
        [Key]
        public Guid Id { get; set; }
        [Required]
        public string Title { get; set; }
        public DateTime DateCreated { get; set; }
    }
}
