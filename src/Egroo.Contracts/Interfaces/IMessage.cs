using Egroo.Contracts.Models;

namespace Egroo.Contracts.Interfaces
{
    public interface IMessage
    {
        Task<bool> SendMessage(Message message);
    }
    public interface IMessageHub : IMessage
    {
        Task<bool> UpdateMessage(Message message);
        Task UpdatePendingMessage(Guid messageid);
        Task SendPendingMessages();
        Task StartTyping(Guid channelId);
        Task StopTyping(Guid channelId);
    }
    public interface IMessageRepository : IMessage
    {
        Task<bool> SendMessage(Message message, IReadOnlyCollection<MessageRecipientContent> deliveries);
        Task<(Message? Message, bool Changed)> MarkMessageSeen(Guid readerId, Guid messageId, Guid referenceId);
        Task UpdatePendingMessage(Guid userId, Guid messageId);
        Task<Message?> GetMessageByReferenceId(Guid referenceId);
        Task<IEnumerable<UserPendingMessage>> GetPendingMessagesForUser(Guid userId);
        Task<IReadOnlyList<(Message Message, string? Content)>> GetPendingMessageDeliveries(Guid userId);
        Task<Message?> GetMessageById(Guid messageId);
        Task<bool> AddPendingMessage(UserPendingMessage pendingMessage);
        string DecryptContent(string content);
    }
}
