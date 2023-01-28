using AutoMapper;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

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
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = await GetDbSet(dbContext).FindAsync(id);
            return Mapper.Map<T>(entity);
        }
    }

    public virtual async Task<T> CreateAsync(T obj)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            obj.SetNewId();
            var entity = Mapper.Map<TEntity>(obj);
            await dbContext.AddAsync(entity);
            await dbContext.SaveChangesAsync();
            obj.Id = entity.Id;
            return obj;
        }
    }

    public virtual async Task ReplaceAsync(T obj)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = await GetDbSet(dbContext).FindAsync(obj.Id);
            if (entity != null)
            {
                var mappedEntity = Mapper.Map<TEntity>(obj);
                dbContext.Entry(entity).CurrentValues.SetValues(mappedEntity);
                await dbContext.SaveChangesAsync();
            }
        }
    }

    public virtual async Task UpsertAsync(T obj)
    {
        if (obj.Id.Equals(default(TId)))
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
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = Mapper.Map<TEntity>(obj);
            dbContext.Remove(entity);
            await dbContext.SaveChangesAsync();
        }
    }

    public virtual async Task RefreshDb()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var context = GetDatabaseContext(scope);
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }
    }

    public virtual async Task<List<T>> CreateMany(List<T> objs)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var entities = new List<TEntity>();
            foreach (var o in objs)
            {
                o.SetNewId();
                var entity = Mapper.Map<TEntity>(o);
                entities.Add(entity);
            }
            var dbContext = GetDatabaseContext(scope);
            await GetDbSet(dbContext).AddRangeAsync(entities);
            await dbContext.SaveChangesAsync();
            return objs;
        }
    }

    public IQueryable<Tout> Run<Tout>(IQuery<Tout> query)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            return query.Run(dbContext);
        }
    }
}
