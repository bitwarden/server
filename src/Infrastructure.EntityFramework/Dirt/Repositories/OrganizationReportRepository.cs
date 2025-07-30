// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Repositories;
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

    public async Task<OrganizationReport> GetLatestByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var result = await dbContext.OrganizationReports
                .Where(p => p.OrganizationId == organizationId)
                .OrderByDescending(p => p.RevisionDate)
                .Take(1)
                .FirstOrDefaultAsync();

            if (result == null)
                return default;

            return Mapper.Map<OrganizationReport>(result);
        }
    }
}
