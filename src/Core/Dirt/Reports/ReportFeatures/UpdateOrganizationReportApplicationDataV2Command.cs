using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class UpdateOrganizationReportApplicationDataV2Command : IUpdateOrganizationReportApplicationDataV2Command
{
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<UpdateOrganizationReportApplicationDataV2Command> _logger;

    public UpdateOrganizationReportApplicationDataV2Command(
        IOrganizationReportRepository organizationReportRepository,
        ILogger<UpdateOrganizationReportApplicationDataV2Command> logger)
    {
        _organizationReportRepo = organizationReportRepository;
        _logger = logger;
    }

    public async Task<OrganizationReport> UpdateApplicationDataAsync(
        UpdateOrganizationReportApplicationDataRequest request)
    {
        var existingReport = await _organizationReportRepo.GetByIdAsync(request.Id);
        if (existingReport == null)
        {
            throw new NotFoundException("Organization report not found");
        }

        if (existingReport.OrganizationId != request.OrganizationId)
        {
            throw new NotFoundException("Organization report not found");
        }

        return await _organizationReportRepo.UpdateApplicationDataAsync(
            request.OrganizationId, request.Id, request.ApplicationData!);
    }
}
