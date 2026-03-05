using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Dirt.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class ValidateOrganizationReportFileCommand : IValidateOrganizationReportFileCommand
{
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly IOrganizationReportStorageService _storageService;
    private readonly ILogger<ValidateOrganizationReportFileCommand> _logger;

    public ValidateOrganizationReportFileCommand(
        IOrganizationReportRepository organizationReportRepo,
        IOrganizationReportStorageService storageService,
        ILogger<ValidateOrganizationReportFileCommand> logger)
    {
        _organizationReportRepo = organizationReportRepo;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<bool> ValidateAsync(OrganizationReport report, string reportFileId)
    {
        var fileData = report.GetReportFile();
        if (fileData == null || fileData.Id != reportFileId)
        {
            return false;
        }

        var (valid, length) = await _storageService.ValidateFileAsync(report, fileData, 0, Constants.FileSize501mb);
        if (!valid)
        {
            _logger.LogWarning(
                "Deleted report {ReportId} because its file size {Size} was invalid.",
                report.Id, length);
            await _storageService.DeleteReportFilesAsync(report, reportFileId);
            await _organizationReportRepo.DeleteAsync(report);
            return false;
        }

        fileData.Validated = true;
        fileData.Size = length;
        report.SetReportFile(fileData);
        report.RevisionDate = DateTime.UtcNow;
        await _organizationReportRepo.ReplaceAsync(report);
        return true;
    }
}
