using System;
using System.ComponentModel.DataAnnotations;

namespace JihadKhawaja.SignalR.Server.Chat.Models
{
    public class Channel
    {
        [Key]
        public Guid Id { get; set; }
        public DateTime DateCreated { get; set; }
    }
}
