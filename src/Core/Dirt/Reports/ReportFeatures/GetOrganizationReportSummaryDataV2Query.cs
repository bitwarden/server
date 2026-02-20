using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class GetOrganizationReportSummaryDataV2Query : IGetOrganizationReportSummaryDataV2Query
{
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<GetOrganizationReportSummaryDataV2Query> _logger;

    public GetOrganizationReportSummaryDataV2Query(
        IOrganizationReportRepository organizationReportRepo,
        ILogger<GetOrganizationReportSummaryDataV2Query> logger)
    {
        _organizationReportRepo = organizationReportRepo;
        _logger = logger;
    }

    public async Task<OrganizationReportSummaryDataResponse?> GetSummaryDataAsync(
        Guid organizationId, Guid reportId)
    {
        var report = await _organizationReportRepo.GetByIdAsync(reportId);
        if (report == null || report.OrganizationId != organizationId)
        {
            return null;
        }

        return new OrganizationReportSummaryDataResponse
        {
            SummaryData = report.SummaryData
        };
    }
}
