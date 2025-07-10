using AutoMapper;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;

public class OrganizationIntegrationRepository : Repository<Core.AdminConsole.Entities.OrganizationIntegration, OrganizationIntegration, Guid>, IOrganizationIntegrationRepository
{
    public OrganizationIntegrationRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.OrganizationIntegrations)
    { }
}
