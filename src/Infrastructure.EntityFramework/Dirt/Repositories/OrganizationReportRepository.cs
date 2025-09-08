// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
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

            if (result == null) return default;

            return Mapper.Map<OrganizationReport>(result);
        }
    }

    public async Task<OrganizationReport> UpdateSummaryDataAsync(Guid orgId, Guid reportId, string summaryData)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            // Update only SummaryData and RevisionDate
            await dbContext.OrganizationReports
                .Where(p => p.Id == reportId && p.OrganizationId == orgId)
                .UpdateAsync(p => new Models.OrganizationReport
                {
                    SummaryData = summaryData,
                    RevisionDate = DateTime.UtcNow
                });

            // Return the updated report
            var updatedReport = await dbContext.OrganizationReports
                .Where(p => p.Id == reportId)
                .FirstOrDefaultAsync();

            return Mapper.Map<OrganizationReport>(updatedReport);
        }
    }

    public async Task<OrganizationReportSummaryDataResponse> GetSummaryDataAsync(Guid reportId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var result = await dbContext.OrganizationReports
                .Where(p => p.Id == reportId)
                .Select(p => new OrganizationReportSummaryDataResponse
                {
                    SummaryData = p.SummaryData
                })
                .FirstOrDefaultAsync();

            return result;
        }
    }

    public async Task<IEnumerable<OrganizationReportSummaryDataResponse>> GetSummaryDataByDateRangeAsync(
        Guid organizationId,
        DateTime startDate,
        DateTime endDate)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var results = await dbContext.OrganizationReports
                .Where(p => p.OrganizationId == organizationId &&
                            p.CreationDate >= startDate && p.CreationDate <= endDate)
                .Select(p => new OrganizationReportSummaryDataResponse
                {
                    SummaryData = p.SummaryData
                })
                .ToListAsync();

            return results;
        }
    }

    public async Task<OrganizationReportDataResponse> GetReportDataAsync(Guid reportId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var result = await dbContext.OrganizationReports
                .Where(p => p.Id == reportId)
                .Select(p => new OrganizationReportDataResponse
                {
                    ReportData = p.ReportData
                })
                .FirstOrDefaultAsync();

            return result;
        }
    }

    public async Task<OrganizationReport> UpdateReportDataAsync(Guid orgId, Guid reportId, string reportData)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            // Update only ReportData and RevisionDate
            await dbContext.OrganizationReports
                .Where(p => p.Id == reportId && p.OrganizationId == orgId)
                .UpdateAsync(p => new Models.OrganizationReport
                {
                    ReportData = reportData,
                    RevisionDate = DateTime.UtcNow
                });

            // Return the updated report
            var updatedReport = await dbContext.OrganizationReports
                .Where(p => p.Id == reportId)
                .FirstOrDefaultAsync();

            return Mapper.Map<OrganizationReport>(updatedReport);
        }
    }

    public async Task<OrganizationReportApplicationDataResponse> GetApplicationDataAsync(Guid reportId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var result = await dbContext.OrganizationReports
                .Where(p => p.Id == reportId)
                .Select(p => new OrganizationReportApplicationDataResponse
                {
                    ApplicationData = p.ApplicationData
                })
                .FirstOrDefaultAsync();

            return result;
        }
    }

    public async Task<OrganizationReport> UpdateApplicationDataAsync(Guid orgId, Guid reportId, string applicationData)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            // Update only ApplicationData and RevisionDate
            await dbContext.OrganizationReports
                .Where(p => p.Id == reportId && p.OrganizationId == orgId)
                .UpdateAsync(p => new Models.OrganizationReport
                {
                    ApplicationData = applicationData,
                    RevisionDate = DateTime.UtcNow
                });

            // Return the updated report
            var updatedReport = await dbContext.OrganizationReports
                .Where(p => p.Id == reportId)
                .FirstOrDefaultAsync();

            return Mapper.Map<OrganizationReport>(updatedReport);
        }
    }
}
