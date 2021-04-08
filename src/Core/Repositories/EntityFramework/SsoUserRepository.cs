using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Models.Table;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;
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
                await DeleteAsync(dbContext, userId, organizationId);
            }
        }

        internal async Task DeleteAsync(DatabaseContext dbContext, Guid userId, Guid? organizationId)
        {
            var entity = await GetDbSet(dbContext).SingleOrDefaultAsync(su => su.UserId == userId && su.OrganizationId == organizationId);
            dbContext.Entry(entity).State = EntityState.Deleted;
            await dbContext.SaveChangesAsync();
        }
    }
}
