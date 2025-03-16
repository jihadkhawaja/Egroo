using jihadkhawaja.chat.shared.Models;

namespace jihadkhawaja.chat.shared.Interfaces
{
    public interface IMessage
    {
        Task<bool> SendMessage(Message message);
        Task<bool> UpdateMessage(Message message);
        Task UpdatePendingMessage(Guid messageid);
    }
    public interface IMessageHub : IMessage
    {
        Task SendPendingMessages();
    }
    public interface IMessageRepository : IMessage
    {
        Task<Message?> GetMessageByReferenceId(Guid referenceId);
        Task<IEnumerable<UserPendingMessage>> GetPendingMessagesForUser(Guid userId);
        Task<Message?> GetMessageById(Guid messageId);
        Task<bool> AddPendingMessage(UserPendingMessage pendingMessage);
    }
}
