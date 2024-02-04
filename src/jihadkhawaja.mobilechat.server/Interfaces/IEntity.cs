namespace jihadkhawaja.mobilechat.server.Interfaces
{
    public interface IEntity<T>
    {
        Task<bool> Create(IEnumerable<T> entities);
        Task<bool> CreateOrUpdate(IEnumerable<T> entities);
        Task<T> ReadFirst(Func<T, bool> predicate, string childName = null);
        Task<T> ReadHighest(Func<T, object> predicate, string childName = null);
        Task<T> ReadLowest(Func<T, object> predicate, string childName = null);
        Task<IEnumerable<T>> Read(Func<T, bool> predicate, string childName = null);
        Task<bool> HasAny(Func<T, bool> predicate);
        Task<bool> Update(IEnumerable<T> newentities);
        Task<bool> Delete(Func<T, bool> predicate);
    }
}
