// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Dirt.Repositories;

public class OrganizationReportSummaryRepository :
    Repository<OrganizationReportSummary, Models.OrganizationReportSummary, Guid>,
    IOrganizationReportSummaryRepository
{
    public OrganizationReportSummaryRepository(IServiceScopeFactory scopeFactory,
        IMapper mapper) : base(scopeFactory, mapper, (DatabaseContext context) => context.OrganizationReportSummaries)
    { }

    public async Task<ICollection<OrganizationReportSummary>> GetByOrganizationReportIdAsync(Guid organizationReportId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var result = await dbContext.OrganizationReportSummaries
                .Where(p => p.OrganizationReportId == organizationReportId)
                .ToListAsync();

            return Mapper.Map<ICollection<OrganizationReportSummary>>(result);
        }
    }
}
