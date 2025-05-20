using AutoMapper;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Infrastructure.EntityFramework.Dirt.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Dirt.Repositories;

public class RiskInsightCriticalApplicationRepository :
    Repository<Core.Dirt.Reports.Entities.RiskInsightCriticalApplication, RiskInsightCriticalApplication, Guid>,
    IRiskInsightCriticalApplicationRepository
{
    public RiskInsightCriticalApplicationRepository(IServiceScopeFactory serviceScopeFactory,
        IMapper mapper) : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.RiskInsightCriticalApplications)
    { }

    public async Task<ICollection<Core.Dirt.Reports.Entities.RiskInsightCriticalApplication>> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.RiskInsightCriticalApplications
                .Where(p => p.OrganizationId == organizationId)
                .ToListAsync();
            return Mapper.Map<ICollection<Core.Dirt.Reports.Entities.RiskInsightCriticalApplication>>(results);
        }
    }
}
