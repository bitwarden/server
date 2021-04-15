using System;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class SsoUserRepository : Repository<TableModel.SsoUser, EfModel.SsoUser, long>, ISsoUserRepository
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
    }
}
