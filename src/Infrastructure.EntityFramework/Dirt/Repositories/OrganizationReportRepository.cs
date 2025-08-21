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

    public async Task<OrganizationReport> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var result = await dbContext.OrganizationReports
                .Where(p => p.OrganizationId == organizationId)
                .FirstOrDefaultAsync();

            if (result == null) return default;

            return Mapper.Map<OrganizationReport>(result);

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

            if (result == null) return default;

            return Mapper.Map<OrganizationReport>(result);
        }
    }

    public async Task<OrganizationReport> UpdateSummaryDataAsync(Guid reportId, string summaryData)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            // Update only SummaryData and RevisionDate
            await dbContext.OrganizationReports
                .Where(p => p.Id == reportId)
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

    public async Task<OrganizationReportSummaryDataResponse> GetSummaryDataAsync(Guid organizationId, Guid reportId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var result = await dbContext.OrganizationReports
                .Where(p => p.OrganizationId == organizationId && p.Id == reportId)
                .Select(p => new OrganizationReportSummaryDataResponse
                {
                    Id = p.Id,
                    OrganizationId = p.OrganizationId,
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
                    Id = p.Id,
                    OrganizationId = p.OrganizationId,
                    SummaryData = p.SummaryData
                })
                .ToListAsync();

            return results;
        }
    }

    public async Task<OrganizationReportDataResponse> GetReportDataAsync(Guid organizationId, Guid reportId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var result = await dbContext.OrganizationReports
                .Where(p => p.OrganizationId == organizationId && p.Id == reportId)
                .Select(p => new OrganizationReportDataResponse
                {
                    Id = p.Id,
                    OrganizationId = p.OrganizationId,
                    ReportData = p.ReportData
                })
                .FirstOrDefaultAsync();

            return result;
        }
    }

    public async Task<OrganizationReport> UpdateReportDataAsync(Guid organizationId, Guid reportId, string reportData)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            // Update only ReportData and RevisionDate
            await dbContext.OrganizationReports
                .Where(p => p.OrganizationId == organizationId && p.Id == reportId)
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

    public async Task<OrganizationReportApplicationDataResponse> GetApplicationDataAsync(Guid organizationId, Guid reportId)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            var result = await dbContext.OrganizationReports
                .Where(p => p.OrganizationId == organizationId && p.Id == reportId)
                .Select(p => new OrganizationReportApplicationDataResponse
                {
                    Id = p.Id,
                    OrganizationId = p.OrganizationId,
                    ApplicationData = p.ApplicationData
                })
                .FirstOrDefaultAsync();

            return result;
        }
    }

    public async Task<OrganizationReport> UpdateApplicationDataAsync(Guid organizationId, Guid reportId, string applicationData)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);

            // Update only ApplicationData and RevisionDate
            await dbContext.OrganizationReports
                .Where(p => p.OrganizationId == organizationId && p.Id == reportId)
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
