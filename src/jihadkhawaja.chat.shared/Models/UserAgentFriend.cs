using System.ComponentModel.DataAnnotations;

namespace jihadkhawaja.chat.shared.Models
{
    /// <summary>
    /// Represents a friendship between a user and a published AI agent.
    /// Once a user adds a published agent as a friend, they can invite it to channels.
    /// </summary>
    public class UserAgentFriend : EntityBase
    {
        [Required]
        public Guid UserId { get; set; }

        [Required]
        public Guid AgentDefinitionId { get; set; }
    }
}
