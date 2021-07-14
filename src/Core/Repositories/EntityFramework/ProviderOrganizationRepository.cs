using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TableModel = Bit.Core.Models.Table;
using EfModel = Bit.Core.Models.EntityFramework;
using Microsoft.Extensions.DependencyInjection;
using AutoMapper;
using Bit.Core.Models.Data;
using Bit.Core.Repositories.EntityFramework.Queries;
using Microsoft.EntityFrameworkCore;

namespace Bit.Core.Repositories.EntityFramework
{
    public class ProviderOrganizationRepository : 
        Repository<TableModel.Provider.ProviderOrganization, EfModel.Provider.ProviderOrganization, Guid>, IProviderOrganizationRepository
    {
        public ProviderOrganizationRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, context => context.ProviderOrganizations)
        { }

        public async Task<ICollection<ProviderOrganizationOrganizationDetails>> GetManyDetailsByProviderAsync(Guid providerId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var query = new ProviderOrganizationOrganizationDetailsReadByProviderIdQuery(providerId);
                var data = await query.Run(dbContext).ToListAsync();
                return data;
            }
        }
    }
}
