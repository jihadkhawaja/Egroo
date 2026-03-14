namespace jihadkhawaja.chat.shared.Models
{
    public class ChannelTypingState
    {
        public Guid ChannelId { get; set; }
        public Guid UserId { get; set; }
        public Guid? AgentDefinitionId { get; set; }
        public string? DisplayName { get; set; }
        public bool IsAgent { get; set; }
    }
}