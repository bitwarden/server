using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Repositories.EntityFramework.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class PolicyRepository : Repository<TableModel.Policy, EfModel.Policy, Guid>, IPolicyRepository
    {
        public PolicyRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Policies)
        { }

        public async Task<Policy> GetByOrganizationIdTypeAsync(Guid organizationId, PolicyType type)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.Policies
                    .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.Type == type);
                return Mapper.Map<TableModel.Policy>(results);
            }
        }

        public async Task<ICollection<Policy>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var results = await dbContext.Policies
                    .Where(p => p.OrganizationId == organizationId)
                    .ToListAsync();
                return Mapper.Map<List<TableModel.Policy>>(results);
            }
        }

        public async Task<ICollection<Policy>> GetManyByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);

                var query = new PolicyReadByUserIdQuery(userId);
                var results = await query.Run(dbContext).ToListAsync();
                return Mapper.Map<List<TableModel.Policy>>(results);
            }
        }

        public async Task<ICollection<Policy>> GetManyByTypeApplicableToUserIdAsync(Guid userId, PolicyType policyType,
                    OrganizationUserStatusType minStatus)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);

                var query = new PolicyReadByTypeApplicableToUserQuery(userId, policyType, minStatus);
                var results = await query.Run(dbContext).ToListAsync();
                return Mapper.Map<List<TableModel.Policy>>(results);
            }
        }

        public async Task<int> GetCountByTypeApplicableToUserIdAsync(Guid userId, PolicyType policyType,
                    OrganizationUserStatusType minStatus)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);

                var query = new PolicyReadByTypeApplicableToUserQuery(userId, policyType, minStatus);
                return await GetCountFromQuery(query);
            }
        }
    }
}
