using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class UpdateOrganizationReportApplicationDataFileStorageCommand : IUpdateOrganizationReportApplicationDataFileStorageCommand
{
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly IOrganizationReportStorageService _storageService;
    private readonly ILogger<UpdateOrganizationReportApplicationDataFileStorageCommand> _logger;

    public UpdateOrganizationReportApplicationDataFileStorageCommand(
        IOrganizationReportRepository organizationReportRepository,
        IOrganizationReportStorageService storageService,
        ILogger<UpdateOrganizationReportApplicationDataFileStorageCommand> logger)
    {
        _organizationReportRepo = organizationReportRepository;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<string> GetUploadUrlAsync(UpdateOrganizationReportApplicationDataRequest request, string reportFileId)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Generating upload URL for application data - organization {organizationId}, report {reportId}",
            request.OrganizationId, request.Id);

        var existingReport = await _organizationReportRepo.GetByIdAsync(request.Id);
        if (existingReport == null || existingReport.OrganizationId != request.OrganizationId)
        {
            throw new NotFoundException("Report not found");
        }

        // Update revision date
        existingReport.RevisionDate = DateTime.UtcNow;
        await _organizationReportRepo.ReplaceAsync(existingReport);

        return await _storageService.GetApplicationDataUploadUrlAsync(existingReport, reportFileId);
    }
}
