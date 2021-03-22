using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
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

        public Task<Policy> GetByOrganizationIdTypeAsync(Guid organizationId, PolicyType type)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<Policy>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<Policy>> GetManyByUserIdAsync(Guid userId)
        {
            throw new NotImplementedException();
        }
    }
}
