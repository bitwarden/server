using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class GetOrganizationReportApplicationDataV2Query : IGetOrganizationReportApplicationDataV2Query
{
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<GetOrganizationReportApplicationDataV2Query> _logger;

    public GetOrganizationReportApplicationDataV2Query(
        IOrganizationReportRepository organizationReportRepo,
        ILogger<GetOrganizationReportApplicationDataV2Query> logger)
    {
        _organizationReportRepo = organizationReportRepo;
        _logger = logger;
    }

    public async Task<OrganizationReportApplicationDataResponse?> GetApplicationDataAsync(
        Guid organizationId, Guid reportId)
    {
        var report = await _organizationReportRepo.GetByIdAsync(reportId);
        if (report == null || report.OrganizationId != organizationId)
        {
            return null;
        }

        return new OrganizationReportApplicationDataResponse
        {
            ApplicationData = report.ApplicationData
        };
    }
}
