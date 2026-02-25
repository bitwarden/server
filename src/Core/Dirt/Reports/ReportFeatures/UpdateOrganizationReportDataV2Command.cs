using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class UpdateOrganizationReportDataV2Command : IUpdateOrganizationReportDataV2Command
{
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly IOrganizationReportStorageService _storageService;
    private readonly ILogger<UpdateOrganizationReportDataV2Command> _logger;

    public UpdateOrganizationReportDataV2Command(
        IOrganizationReportRepository organizationReportRepository,
        IOrganizationReportStorageService storageService,
        ILogger<UpdateOrganizationReportDataV2Command> logger)
    {
        _organizationReportRepo = organizationReportRepository;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<string> GetUploadUrlAsync(UpdateOrganizationReportDataRequest request, string reportFileId)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Generating upload URL for report data - organization {organizationId}, report {reportId}",
            request.OrganizationId, request.ReportId);

        var existingReport = await _organizationReportRepo.GetByIdAsync(request.ReportId);
        if (existingReport == null || existingReport.OrganizationId != request.OrganizationId)
        {
            throw new NotFoundException("Report not found");
        }

        if (existingReport.FileId != reportFileId)
        {
            throw new NotFoundException("Report not found");
        }

        // Update revision date
        existingReport.RevisionDate = DateTime.UtcNow;
        await _organizationReportRepo.ReplaceAsync(existingReport);

        return await _storageService.GetReportDataUploadUrlAsync(existingReport, reportFileId);
    }
}
