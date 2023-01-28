using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class SsoUserRepository : Repository<Core.Entities.SsoUser, SsoUser, long>, ISsoUserRepository
{
    public SsoUserRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.SsoUsers)
    { }

    public async Task DeleteAsync(Guid userId, Guid? organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = await GetDbSet(dbContext).SingleOrDefaultAsync(su => su.UserId == userId && su.OrganizationId == organizationId);
            dbContext.Entry(entity).State = EntityState.Deleted;
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<Core.Entities.SsoUser> GetByUserIdOrganizationIdAsync(Guid organizationId, Guid userId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var entity = await GetDbSet(dbContext)
                .FirstOrDefaultAsync(e => e.OrganizationId == organizationId && e.UserId == userId);
            return entity;
        }
    }
}
