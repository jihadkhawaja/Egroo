using jihadkhawaja.mobilechat.server.Database;
using jihadkhawaja.mobilechat.server.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace jihadkhawaja.mobilechat.server.Services
{
    public class EntityService<T> : IEntity<T> where T : class
    {
        private readonly DataContext context;
        public EntityService(DataContext context)
        {
            this.context = context;
        }
        public async Task<bool> Create(IEnumerable<T> entities)
        {
            try
            {
                context.Set<T>().AddRange(entities);
                await context.SaveChangesAsync();

                context.ChangeTracker.Clear();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Error: {0}.\n{1}", ex.Message, ex.InnerException));
            }

            return false;
        }

        public async Task<bool> CreateOrUpdate(IEnumerable<T> entities)
        {
            try
            {
                List<T> toUpdate = new List<T>();
                List<T> toCreate = new List<T>();

                foreach (T entity in entities)
                {
                    var e = await context.Set<T>().FirstOrDefaultAsync(x => x == entity);
                    if (e == null)
                    {
                        toCreate.Add(entity);
                    }
                    else
                    {
                        toUpdate.Add(entity);
                    }
                }

                context.Set<T>().AddRange(toCreate);
                await context.SaveChangesAsync();

                context.ChangeTracker.Clear();
                context.Set<T>().UpdateRange(toUpdate);
                await context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Error: {0}.\n{1}", ex.Message, ex.InnerException));
            }

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
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Error: {0}.\n{1}", ex.Message, ex.InnerException));
            }

            return false;
        }

        public Task<bool> HasAny(Func<T, bool> predicate)
        {
            bool result = context.Set<T>().Any(predicate);

            return Task.FromResult(result);
        }

        public Task<IEnumerable<T>> Read(Func<T, bool> predicate, string childName = null)
        {
            IEnumerable<T> result = default;
            if (string.IsNullOrWhiteSpace(childName))
            {
                result = context.Set<T>().AsNoTracking().Where(predicate).ToHashSet().AsEnumerable();
            }
            else
            {
                result = context.Set<T>().AsNoTracking().Include(childName).Where(predicate).ToHashSet().AsEnumerable();
            }
            return Task.FromResult(result);
        }

        public Task<T> ReadFirst(Func<T, bool> predicate, string childName = null)
        {
            T result = default;
            if (string.IsNullOrWhiteSpace(childName))
            {
                result = context.Set<T>().FirstOrDefault(predicate);
            }
            else
            {
                result = context.Set<T>().Include(childName).FirstOrDefault(predicate);
            }

            return Task.FromResult(result);
        }

        public Task<T> ReadHighest(Func<T, object> predicate, string childName = null)
        {
            T result = default;
            if (string.IsNullOrWhiteSpace(childName))
            {
                result = context.Set<T>().MaxBy(predicate);
            }
            else
            {
                result = context.Set<T>().Include(childName).MaxBy(predicate);
            }

            return Task.FromResult(result);
        }

        public Task<T> ReadLowest(Func<T, object> predicate, string childName = null)
        {
            T result = default;
            if (string.IsNullOrWhiteSpace(childName))
            {
                result = context.Set<T>().MinBy(predicate);
            }
            else
            {
                result = context.Set<T>().Include(childName).MinBy(predicate);
            }

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
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Error: {0}.\n{1}", ex.Message, ex.InnerException));
            }

            return false;
        }
    }
}