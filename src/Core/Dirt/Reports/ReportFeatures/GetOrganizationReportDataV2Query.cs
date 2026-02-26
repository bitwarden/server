using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class GetOrganizationReportDataV2Query : IGetOrganizationReportDataV2Query
{
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly IOrganizationReportStorageService _storageService;
    private readonly ILogger<GetOrganizationReportDataV2Query> _logger;

    public GetOrganizationReportDataV2Query(
        IOrganizationReportRepository organizationReportRepo,
        IOrganizationReportStorageService storageService,
        ILogger<GetOrganizationReportDataV2Query> logger)
    {
        _organizationReportRepo = organizationReportRepo;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<OrganizationReportDataFileStorageResponse> GetOrganizationReportDataAsync(
        Guid organizationId,
        Guid reportId,
        string reportFileId)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Generating download URL for report data - organization {organizationId}, report {reportId}",
            organizationId, reportId);

        if (string.IsNullOrEmpty(reportFileId))
        {
            throw new BadRequestException("ReportFileId is required");
        }

        var report = await _organizationReportRepo.GetByIdAsync(reportId);
        if (report == null || report.OrganizationId != organizationId)
        {
            throw new NotFoundException("Report not found");
        }

        var fileData = report.GetReportFileData();
        if (fileData == null)
        {
            throw new NotFoundException("Report file data not found");
        }

        var downloadUrl = await _storageService.GetReportDataDownloadUrlAsync(report, fileData);

        return new OrganizationReportDataFileStorageResponse { DownloadUrl = downloadUrl };
    }
}
