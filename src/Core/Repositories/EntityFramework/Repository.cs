using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace Bit.Core.Repositories.EntityFramework
{
    public abstract class Repository<T, TEntity, TId> : BaseEntityFrameworkRepository, IRepository<T, TId>
        where TId : IEquatable<TId>
        where T : class, ITableObject<TId>
        where TEntity : class, ITableObject<TId>
    {
        public Repository(DatabaseContext databaseContext, IMapper mapper, Func<DbSet<TEntity>> getDbSet)
            : base(databaseContext, mapper)
        {
            GetDbSet = getDbSet;
        }

        protected Func<DbSet<TEntity>> GetDbSet { get; private set; }

        public virtual async Task<T> GetByIdAsync(TId id)
        {
            var entity = await GetDbSet().FindAsync(id);
            return entity as T;
        }

        public virtual async Task CreateAsync(T obj)
        {
            var entity = Mapper.Map<TEntity>(obj);
            DatabaseContext.Add(entity);
            await DatabaseContext.SaveChangesAsync();
        }

        public virtual async Task ReplaceAsync(T obj)
        {
            var entity = await GetDbSet().FindAsync(obj.Id);
            if(entity != null)
            {
                var mappedEntity = Mapper.Map<TEntity>(obj);
                DatabaseContext.Entry(entity).CurrentValues.SetValues(mappedEntity);
                await DatabaseContext.SaveChangesAsync();
            }
        }

        public virtual async Task UpsertAsync(T obj)
        {
            if(obj.Id.Equals(default(T)))
            {
                await CreateAsync(obj);
            }
            else
            {
                await ReplaceAsync(obj);
            }
        }

        public virtual async Task DeleteAsync(T obj)
        {
            var entity = Mapper.Map<TEntity>(obj);
            DatabaseContext.Entry(entity).State = EntityState.Deleted;
            await DatabaseContext.SaveChangesAsync();
        }
    }
}
