using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Models.Table;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class SsoConfigRepository : Repository<TableModel.SsoConfig, EfModel.SsoConfig, long>, ISsoConfigRepository
    {
        public SsoConfigRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.SsoConfigs)
        { }

        public async Task<SsoConfig> GetByOrganizationIdAsync(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var ssoConfig = await GetDbSet(dbContext).SingleOrDefaultAsync(sc => sc.OrganizationId == organizationId);
                return Mapper.Map<TableModel.SsoConfig>(ssoConfig);
            }
        }

        public async Task<SsoConfig> GetByIdentifierAsync(string identifier)
        {

            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var ssoConfig = await GetDbSet(dbContext).SingleOrDefaultAsync(sc => sc.Organization.Identifier == identifier);
                return Mapper.Map<TableModel.SsoConfig>(ssoConfig);
            }
        }

        public async Task<ICollection<SsoConfig>> GetManyByRevisionNotBeforeDate(DateTime? notBefore)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var ssoConfigs = await GetDbSet(dbContext).Where(sc => sc.Enabled && sc.RevisionDate >= notBefore).ToListAsync();
                return Mapper.Map<List<TableModel.SsoConfig>>(ssoConfigs);
            }
        }
    }
}
