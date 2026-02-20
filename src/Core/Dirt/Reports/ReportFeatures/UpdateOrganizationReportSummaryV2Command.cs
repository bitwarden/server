using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class UpdateOrganizationReportSummaryV2Command : IUpdateOrganizationReportSummaryV2Command
{
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly ILogger<UpdateOrganizationReportSummaryV2Command> _logger;

    public UpdateOrganizationReportSummaryV2Command(
        IOrganizationReportRepository organizationReportRepository,
        ILogger<UpdateOrganizationReportSummaryV2Command> logger)
    {
        _organizationReportRepo = organizationReportRepository;
        _logger = logger;
    }

    public async Task<OrganizationReport> UpdateSummaryAsync(
        UpdateOrganizationReportSummaryRequest request)
    {
        var existingReport = await _organizationReportRepo.GetByIdAsync(request.ReportId);
        if (existingReport == null)
        {
            throw new NotFoundException("Organization report not found");
        }

        if (existingReport.OrganizationId != request.OrganizationId)
        {
            throw new NotFoundException("Organization report not found");
        }

        await _organizationReportRepo.UpdateMetricsAsync(
            request.ReportId, OrganizationReportMetricsData.From(request.OrganizationId, request.ReportMetrics));

        return await _organizationReportRepo.UpdateSummaryDataAsync(
            request.OrganizationId, request.ReportId, request.SummaryData!);
    }
}
