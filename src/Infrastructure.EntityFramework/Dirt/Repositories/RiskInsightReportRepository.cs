using AutoMapper;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Infrastructure.EntityFramework.Dirt.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;


namespace Bit.Infrastructure.EntityFramework.Dirt.Repositories;

public class RiskInsightReportRepository :
    Repository<Core.Dirt.Reports.Entities.RiskInsightReport, RiskInsightReport, Guid>,
    IRiskInsightReportRepository
{
    public RiskInsightReportRepository(IServiceScopeFactory serviceScopeFactory,
        IMapper mapper) : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.RiskInsightReports)
    { }

    public async Task<ICollection<Core.Dirt.Reports.Entities.RiskInsightReport>> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.RiskInsightReports
                .Where(p => p.OrganizationId == organizationId)
                .ToListAsync();
            return Mapper.Map<ICollection<Core.Dirt.Reports.Entities.RiskInsightReport>>(results);
        }
    }
}
