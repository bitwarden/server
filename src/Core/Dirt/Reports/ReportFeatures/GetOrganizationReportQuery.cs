using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class GetOrganizationReportQuery : IGetOrganizationReportQuery
{
    private IOrganizationReportRepository _organizationReportRepo;
    private ILogger<GetOrganizationReportQuery> _logger;

    public GetOrganizationReportQuery(
        IOrganizationReportRepository organizationReportRepo,
        ILogger<GetOrganizationReportQuery> logger)
    {
        _organizationReportRepo = organizationReportRepo;
        _logger = logger;
    }

    public async Task<OrganizationReport> GetOrganizationReportAsync(Guid reportId)
    {
        if (reportId == Guid.Empty)
        {
            throw new BadRequestException("Id of report is required.");
        }

        _logger.LogInformation(Constants.BypassFiltersEventId, "Fetching organization reports for organization by Id: {reportId}", reportId);

        var results = await _organizationReportRepo.GetByIdAsync(reportId);

        if (results == null)
        {
            throw new NotFoundException($"No report found for Id: {reportId}");
        }

        return results;
    }

    public async Task<OrganizationReport> GetLatestOrganizationReportAsync(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new BadRequestException("OrganizationId is required.");
        }

        _logger.LogInformation(Constants.BypassFiltersEventId, "Fetching latest organization report for organization {organizationId}", organizationId);
        return await _organizationReportRepo.GetLatestByOrganizationIdAsync(organizationId);
    }
}
