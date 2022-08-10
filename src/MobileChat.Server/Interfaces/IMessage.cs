using MobileChat.Shared.Models;

namespace MobileChat.Server.Interfaces
{
    public interface IMessage
    {
        //crud
        Task<bool> Create(Message entry);
        Task<Message> ReadById(Guid id);
        Task<bool> Update(Message entry);
        Task<bool> Delete(Guid id);
    }
}
