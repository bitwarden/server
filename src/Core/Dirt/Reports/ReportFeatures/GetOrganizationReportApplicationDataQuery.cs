using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class GetOrganizationReportApplicationDataQuery : IGetOrganizationReportApplicationDataQuery
{
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<GetOrganizationReportApplicationDataQuery> _logger;

    public GetOrganizationReportApplicationDataQuery(
        IOrganizationReportRepository organizationReportRepo,
        ILogger<GetOrganizationReportApplicationDataQuery> logger)
    {
        _organizationReportRepo = organizationReportRepo;
        _logger = logger;
    }

    public async Task<OrganizationReportApplicationDataResponse> GetOrganizationReportApplicationDataAsync(Guid organizationId, Guid reportId)
    {
        var applicationDataResponse = await _organizationReportRepo.GetApplicationDataAsync(reportId);

        return applicationDataResponse;
    }
}
