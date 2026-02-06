using AutoMapper;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Auth.Repositories;

public class GrantRepository : BaseEntityFrameworkRepository, IGrantRepository
{
    public GrantRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper)
    { }

    public async Task DeleteByKeyAsync(string key)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.Grants.Where(g => g.Key == key).ExecuteDeleteAsync();
        }
    }

    public async Task DeleteManyAsync(string subjectId, string sessionId, string clientId, string type)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            await dbContext.Grants.Where(g =>
                g.SubjectId == subjectId &&
                g.ClientId == clientId &&
                g.SessionId == sessionId &&
                g.Type == type).ExecuteDeleteAsync();
        }
    }

    public async Task<IGrant?> GetByKeyAsync(string key)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from g in dbContext.Grants
                        where g.Key == key
                        select g;
            var grant = await query.FirstOrDefaultAsync();
            return grant;
        }
    }

    public async Task<ICollection<IGrant>> GetManyAsync(string subjectId, string sessionId, string clientId, string type)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from g in dbContext.Grants
                        where g.SubjectId == subjectId &&
                            g.ClientId == clientId &&
                            g.SessionId == sessionId &&
                            g.Type == type
                        select g;
            var grants = await query.ToListAsync();
            return (ICollection<IGrant>)grants;
        }
    }

    public async Task SaveAsync(IGrant obj)
    {
        if (obj is not Core.Auth.Entities.Grant gObj)
        {
            throw new ArgumentException(null, nameof(obj));
        }

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var existingGrant = await (from g in dbContext.Grants
                                       where g.Key == gObj.Key
                                       select g).FirstOrDefaultAsync();
            if (existingGrant != null)
            {
                gObj.Id = existingGrant.Id;
                dbContext.Entry(existingGrant).CurrentValues.SetValues(gObj);
            }
            else
            {
                var entity = Mapper.Map<Grant>(gObj);
                await dbContext.AddAsync(entity);
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
