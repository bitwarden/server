using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

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
            var query = from g in dbContext.Grants
                        where g.Key == key
                        select g;
            dbContext.Remove(query);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task DeleteManyAsync(string subjectId, string sessionId, string clientId, string type)
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
            dbContext.Remove(query);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<Core.Entities.Grant> GetByKeyAsync(string key)
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

    public async Task<ICollection<Core.Entities.Grant>> GetManyAsync(string subjectId, string sessionId, string clientId, string type)
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
            return (ICollection<Core.Entities.Grant>)grants;
        }
    }

    public async Task SaveAsync(Core.Entities.Grant obj)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var existingGrant = await (from g in dbContext.Grants
                                       where g.Key == obj.Key
                                       select g).FirstOrDefaultAsync();
            if (existingGrant != null)
            {
                dbContext.Entry(existingGrant).CurrentValues.SetValues(obj);
            }
            else
            {
                var entity = Mapper.Map<Grant>(obj);
                await dbContext.AddAsync(entity);
                await dbContext.SaveChangesAsync();
            }
        }
    }
}

