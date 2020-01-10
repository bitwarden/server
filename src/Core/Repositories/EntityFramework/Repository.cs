using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Repositories.EntityFramework
{
    public abstract class Repository<T, TEntity, TId> : BaseEntityFrameworkRepository, IRepository<T, TId>
        where TId : IEquatable<TId>
        where T : class, ITableObject<TId>
        where TEntity : class, ITableObject<TId>
    {
        public Repository(IServiceScopeFactory serviceScopeFactory, IMapper mapper, Func<DatabaseContext, DbSet<TEntity>> getDbSet)
            : base(serviceScopeFactory, mapper)
        {
            GetDbSet = getDbSet;
        }

        protected Func<DatabaseContext, DbSet<TEntity>> GetDbSet { get; private set; }

        public virtual async Task<T> GetByIdAsync(TId id)
        {
            using(var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var entity = await GetDbSet(dbContext).FindAsync(id);
                return entity as T;
            }
        }

        public virtual async Task CreateAsync(T obj)
        {
            using(var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var entity = Mapper.Map<TEntity>(obj);
                dbContext.Add(entity);
                await dbContext.SaveChangesAsync();
                obj.Id = entity.Id;
            }
        }

        public virtual async Task ReplaceAsync(T obj)
        {
            using(var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var entity = await GetDbSet(dbContext).FindAsync(obj.Id);
                if(entity != null)
                {
                    var mappedEntity = Mapper.Map<TEntity>(obj);
                    dbContext.Entry(entity).CurrentValues.SetValues(mappedEntity);
                    await dbContext.SaveChangesAsync();
                }
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
            using(var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var entity = Mapper.Map<TEntity>(obj);
                dbContext.Entry(entity).State = EntityState.Deleted;
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
