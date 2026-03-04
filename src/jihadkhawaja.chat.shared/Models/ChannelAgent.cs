using System.ComponentModel.DataAnnotations;

namespace jihadkhawaja.chat.shared.Models
{
    /// <summary>
    /// Links an AI agent to a channel so it can respond to @mentions.
    /// </summary>
    public class ChannelAgent : EntityBase
    {
        [Required]
        public Guid ChannelId { get; set; }

        [Required]
        public Guid AgentDefinitionId { get; set; }

        /// <summary>
        /// The user who added this agent to the channel.
        /// </summary>
        [Required]
        public Guid AddedByUserId { get; set; }
    }
}
