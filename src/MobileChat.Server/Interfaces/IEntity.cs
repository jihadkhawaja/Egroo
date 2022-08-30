namespace MobileChat.Server.Interfaces
{
    public interface IEntity<T>
    {
        Task<bool> Create(IEnumerable<T> entity);
        Task<IEnumerable<T>> Read(Func<T, bool> predicate);
        Task<bool> Update(IEnumerable<T> newentities);
        Task<bool> Delete(Func<T, bool> predicate);
    }
}
