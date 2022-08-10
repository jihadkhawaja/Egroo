using System.ComponentModel.DataAnnotations;

namespace MobileChat.Shared.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }
        [Required]
        public string ConnectionId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? DisplayName { get; set; }
        public string? About { get; set; }
        public string? AvatarUrl { get; set; }
        [Required]
        public string Username { get; set; }
        public string? Email { get; set; }
        [Required]
        public string Password { get; set; }
        public string? FirebaseToken { get; set; }
        [Required]
        public int Permission { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? DateCreated { get; set; }
    }
}
