using Microsoft.EntityFrameworkCore;
using MobileChat.Server.Database;
using MobileChat.Server.Interfaces;

namespace MobileChat.Server.Services
{
    public class EntityService<T> : IEntity<T> where T : class
    {
        private readonly DataContext context;
        public EntityService(DataContext context)
        {
            this.context = context;
        }
        public Task<bool> Create(T[] entity)
        {
            try
            {
                context.Set<T>().AddRange(entity);
                context.SaveChanges();

                return Task.FromResult(true);
            }
            catch { }

            return Task.FromResult(false);
        }

        public Task Delete(Func<T, bool> predicate)
        {
            var User = context.Set<T>().Where(predicate);
            if (User != null)
            {
                context.Set<T>().RemoveRange(User);
                context.SaveChanges();
            }

            return Task.CompletedTask;
        }

        public Task<IEnumerable<T>> Read(Func<T, bool> predicate)
        {
            var result = context.Set<T>().AsNoTracking().Where(predicate);
            return Task.FromResult(result);
        }

        public Task<bool> Update(T newentity)
        {
            try
            {
                context.Set<T>().Update(newentity);
                context.SaveChanges();

                return Task.FromResult(true);
            }
            catch { }

            return Task.FromResult(false);
        }
    }
}
