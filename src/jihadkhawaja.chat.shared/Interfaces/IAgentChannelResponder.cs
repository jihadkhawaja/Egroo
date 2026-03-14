using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    /// <summary>
    /// Interface for processing agent mentions in channel messages.
    /// Implemented in the server project and injected into the ChatHub.
    /// </summary>
    public interface IAgentChannelResponder
    {
        /// <summary>
        /// Persist agent-targeted encrypted envelopes for a channel message.
        /// </summary>
        Task PersistAgentRecipientContentsAsync(Message message);

        /// <summary>
        /// Process a channel message for @agent mentions and trigger responses.
        /// </summary>
        Task ProcessMentionsAsync(Message message);
    }
}
