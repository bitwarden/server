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

    public virtual async Task<T?> GetByIdAsync(TId id)
    {
        return await ExecuteWithContextAsync(async dbContext =>
        {
            var entity = await GetDbSet(dbContext).FindAsync(id);
            return Mapper.Map<T>(entity);
        });
    }

    public virtual async Task<T> CreateAsync(T obj)
    {
        return await ExecuteWithContextAsync(async dbContext =>
        {
            obj.SetNewId();
            var entity = Mapper.Map<TEntity>(obj);
            await dbContext.AddAsync(entity);
            await dbContext.SaveChangesAsync();
            obj.Id = entity.Id;
            return obj;
        });
    }

    public virtual async Task ReplaceAsync(T obj)
    {
        await ExecuteWithContextAsync(async dbContext =>
        {
            var entity = await GetDbSet(dbContext).FindAsync(obj.Id);
            if (entity != null)
            {
                var mappedEntity = Mapper.Map<TEntity>(obj);
                dbContext.Entry(entity).CurrentValues.SetValues(mappedEntity);
                await dbContext.SaveChangesAsync();
            }
        });
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
        await ExecuteWithContextAsync(async dbContext =>
        {
            var entity = Mapper.Map<TEntity>(obj);
            dbContext.Remove(entity);
            await dbContext.SaveChangesAsync();
        });
    }

    public virtual async Task RefreshDb()
    {
        await ExecuteWithContextAsync(async dbContext =>
        {
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
        });
    }

    public virtual async Task<List<T>> CreateMany(List<T> objs)
    {
        return await ExecuteWithContextAsync(async dbContext =>
        {
            var entities = new List<TEntity>();
            foreach (var o in objs)
            {
                o.SetNewId();
                var entity = Mapper.Map<TEntity>(o);
                entities.Add(entity);
            }
            await GetDbSet(dbContext).AddRangeAsync(entities);
            await dbContext.SaveChangesAsync();
            return objs;
        });
    }

    public IQueryable<Tout> Run<Tout>(IQuery<Tout> query)
    {
        var (dbContext, ownedScope) = GetDatabaseContextOrAmbient();
        // Note: IQueryable is deferred, so disposing the scope here would break it.
        // This matches the existing behavior where the scope is disposed before
        // the query is materialized. Callers must materialize within scope.
        if (ownedScope is not null)
        {
            // Fall back to original behavior for non-transactional context
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var context = GetDatabaseContext(scope);
                return query.Run(context);
            }
        }
        return query.Run(dbContext);
    }
}
