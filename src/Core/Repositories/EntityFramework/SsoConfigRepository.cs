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
    public class SsoConfigRepository : Repository<TableModel.SsoConfig, EfModel.SsoConfig, long>, ISsoConfigRepository
    {
        public SsoConfigRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.SsoConfigs)
        { }

        public Task<SsoConfig> GetByIdentifierAsync(string identifier)
        {
            throw new NotImplementedException();
        }

        public Task<SsoConfig> GetByOrganizationIdAsync(Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<SsoConfig>> GetManyByRevisionNotBeforeDate(DateTime? notBefore)
        {
            throw new NotImplementedException();
        }
    }
}
