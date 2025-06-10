using AutoMapper;
using Bit.Core.Dirt.Reports.Entities;
using Bit.Core.Dirt.Reports.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;


namespace Bit.Infrastructure.EntityFramework.Dirt.Repositories;

public class OrganizationReportRepository :
    Repository<OrganizationReport, Models.OrganizationReport, Guid>,
    IOrganizationReportRepository
{
    public OrganizationReportRepository(IServiceScopeFactory serviceScopeFactory,
        IMapper mapper) : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.OrganizationReports)
    { }

    public async Task<ICollection<OrganizationReport>> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var results = await dbContext.OrganizationReports
                .Where(p => p.OrganizationId == organizationId)
                .ToListAsync();
            return Mapper.Map<ICollection<OrganizationReport>>(results);
        }
    }
}
