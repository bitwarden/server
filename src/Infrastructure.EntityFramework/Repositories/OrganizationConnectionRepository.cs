using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories
{
    public class OrganizationConnectionRepository : Repository<Core.Entities.OrganizationConnection, OrganizationConnection, Guid>, IOrganizationConnectionRepository
    {
        public OrganizationConnectionRepository(IServiceScopeFactory serviceScopeFactory,
            IMapper mapper)
            : base(serviceScopeFactory, mapper, context => context.OrganizationConnections)
        {
        }

        public async Task<ICollection<Core.Entities.OrganizationConnection>> GetEnabledByOrganizationIdTypeAsync(Guid organizationId, OrganizationConnectionType type)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                return await dbContext.OrganizationConnections
                    .Where(oc => oc.OrganizationId == organizationId && oc.Type == type && oc.Enabled)
                    .Select(oc => Mapper.Map<Core.Entities.OrganizationConnection>(oc))
                    .ToListAsync();
            }
        }
    }
}
