using System;
using System.ComponentModel.DataAnnotations;

namespace JihadKhawaja.SignalR.Server.Chat.Models
{
    public class ChannelUser
    {
        [Key]
        public ulong Id { get; set; }
        [Required]
        public Guid ChannelId { get; set; }
        [Required]
        public Guid UserId { get; set; }
        public DateTime DateCreated { get; set; }
    }
}
