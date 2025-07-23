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

    public async Task<IEnumerable<OrganizationReport>> GetOrganizationReportAsync(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new BadRequestException("OrganizationId is required.");
        }

        _logger.LogInformation("Fetching organization reports for organization {organizationId}", organizationId);
        return await _organizationReportRepo.GetByOrganizationIdAsync(organizationId);
    }

    public async Task<OrganizationReport> GetLatestOrganizationReportAsync(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new BadRequestException("OrganizationId is required.");
        }

        _logger.LogInformation("Fetching latest organization report for organization {organizationId}", organizationId);
        return await _organizationReportRepo.GetLatestByOrganizationIdAsync(organizationId);
    }
}
