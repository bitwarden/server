using AutoMapper;
using Bit.Core.ActionableInsights.Entities;
using Bit.Core.ActionableInsights.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Commercial.Infrastructure.EntityFramework.ActionableInsights.Repositories;

public class ReportRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
    : Repository<Report, Report, Guid>(serviceScopeFactory, mapper, db => db.Report),
        IReportRepository
{
    public async Task<Report?> GetByOrganizationIdAsync(Guid organizationId)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var dbContext = GetDatabaseContext(scope);
        var report = await dbContext.Report
            .Where(c => c.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
        return Mapper.Map<Report>(report);
    }
}
