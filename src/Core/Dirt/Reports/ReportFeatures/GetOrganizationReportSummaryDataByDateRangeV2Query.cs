using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class GetOrganizationReportSummaryDataByDateRangeV2Query : IGetOrganizationReportSummaryDataByDateRangeV2Query
{
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<GetOrganizationReportSummaryDataByDateRangeV2Query> _logger;

    public GetOrganizationReportSummaryDataByDateRangeV2Query(
        IOrganizationReportRepository organizationReportRepo,
        ILogger<GetOrganizationReportSummaryDataByDateRangeV2Query> logger)
    {
        _organizationReportRepo = organizationReportRepo;
        _logger = logger;
    }

    public async Task<IEnumerable<OrganizationReportSummaryDataResponse>> GetSummaryDataByDateRangeAsync(
        Guid organizationId, DateTime startDate, DateTime endDate)
    {
        var summaryDataList = await _organizationReportRepo
            .GetSummaryDataByDateRangeAsync(organizationId, startDate, endDate);

        return summaryDataList ?? Enumerable.Empty<OrganizationReportSummaryDataResponse>();
    }
}
