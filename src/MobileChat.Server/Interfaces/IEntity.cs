namespace MobileChat.Server.Interfaces
{
    public interface IEntity<T>
    {
        Task<bool> Create(T[] entity);
        Task<IEnumerable<T>> Read(Func<T, bool> predicate);
        Task<bool> Update(T newentity);
        Task Delete(Func<T, bool> predicate);
    }
}
