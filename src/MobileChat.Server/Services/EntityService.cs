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
        public async Task<bool> Create(IEnumerable<T> entity)
        {
            try
            {
                context.Set<T>().AddRange(entity);
                await context.SaveChangesAsync();

                return true;
            }
            catch { }

            return false;
        }

        public async Task<bool> Delete(Func<T, bool> predicate)
        {
            try
            {
                IEnumerable<T> User = context.Set<T>().Where(predicate);
                if (User != null)
                {
                    context.Set<T>().RemoveRange(User);
                    await context.SaveChangesAsync();
                }

                return true;
            }
            catch { }

            return false;
        }

        public Task<IEnumerable<T>> Read(Func<T, bool> predicate)
        {
            IEnumerable<T> result = context.Set<T>().AsNoTracking().Where(predicate).ToHashSet().AsEnumerable();
            return Task.FromResult(result);
        }

        public async Task<bool> Update(IEnumerable<T> newentities)
        {
            try
            {
                context.ChangeTracker.Clear();
                context.Set<T>().UpdateRange(newentities);
                await context.SaveChangesAsync();

                return true;
            }
            catch { }

            return false;
        }
    }
}
