using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace jihadkhawaja.chat.shared.Models
{
    public class Message : EntityBase
    {
        [Required]
        public Guid SenderId { get; set; }
        [Required]
        public Guid ChannelId { get; set; }
        [Required]
        public Guid ReferenceId { get; set; } = Guid.NewGuid();
        public DateTimeOffset? DateSent { get; set; }
        public DateTimeOffset? DateSeen { get; set; }
        /// <summary>
        /// When set, this message was sent by an AI agent rather than a human user.
        /// </summary>
        public Guid? AgentDefinitionId { get; set; }
        [NotMapped]
        public string? DisplayName { get; set; }
        [NotMapped]
        public string? Content { get; set; }
        [NotMapped]
        public string? DecryptedContent { get; set; }
        [NotMapped]
        public List<MessageRecipientContent>? RecipientContents { get; set; }
    }

    public class MessageRecipientContent
    {
        [Required]
        public Guid UserId { get; set; }
        [Required]
        public string Content { get; set; } = string.Empty;
    }
}
