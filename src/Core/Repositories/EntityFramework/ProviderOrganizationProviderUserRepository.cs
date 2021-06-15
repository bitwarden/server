using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table.Provider;
using Bit.Core.Repositories.EntityFramework;
using TableModel = Bit.Core.Models.Table;
using EfModel = Bit.Core.Models.EntityFramework;
using Microsoft.Extensions.DependencyInjection;
using AutoMapper;

namespace Bit.Core.Repositories
{
    public class ProviderOrganizationProviderUserRepository : 
        Repository<TableModel.Provider.ProviderOrganizationProviderUser, EfModel.Provider.ProviderOrganizationProviderUser, Guid>, IProviderOrganizationProviderUserRepository
    {
        public ProviderOrganizationProviderUserRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.ProviderOrganizationProviderUsers)
        { }
    }
}
