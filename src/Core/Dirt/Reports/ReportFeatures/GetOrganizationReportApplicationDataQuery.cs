using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
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
        if (organizationId == Guid.Empty)
        {
            throw new BadRequestException("OrganizationId is required.");
        }

        if (reportId == Guid.Empty)
        {
            throw new BadRequestException("ReportId is required.");
        }

        var report = await _organizationReportRepo.GetByIdAsync(reportId);
        if (report == null || report.OrganizationId != organizationId)
        {
            throw new NotFoundException("Organization report application data not found.");
        }

        var applicationDataResponse = await _organizationReportRepo.GetApplicationDataAsync(reportId);

        if (applicationDataResponse == null)
        {
            throw new NotFoundException("Organization report application data not found.");
        }

        return applicationDataResponse;
    }
}
