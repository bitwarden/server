using AutoMapper;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Infrastructure.EntityFramework.Dirt.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Dirt.Repositories;

public class OrganizationApplicationRepository :
    Repository<Core.Dirt.Reports.Entities.OrganizationApplication, OrganizationApplication, Guid>,
    IOrganizationApplicationRepository
{
    public OrganizationApplicationRepository(IServiceScopeFactory serviceScopeFactory,
        IMapper mapper) : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.OrganizationApplications)
    { }

    public async Task<ICollection<Core.Dirt.Reports.Entities.OrganizationApplication>> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.OrganizationApplications
                .Where(p => p.OrganizationId == organizationId)
                .ToListAsync();
            return Mapper.Map<ICollection<Core.Dirt.Reports.Entities.OrganizationApplication>>(results);
        }
    }
}
