using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Dirt.Reports.Services;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

public class GetOrganizationReportApplicationDataFileStorageQuery : IGetOrganizationReportApplicationDataFileStorageQuery
{
    private readonly IOrganizationReportRepository _organizationReportRepo;
    private readonly IOrganizationReportStorageService _storageService;
    private readonly ILogger<GetOrganizationReportApplicationDataFileStorageQuery> _logger;

    public GetOrganizationReportApplicationDataFileStorageQuery(
        IOrganizationReportRepository organizationReportRepo,
        IOrganizationReportStorageService storageService,
        ILogger<GetOrganizationReportApplicationDataFileStorageQuery> logger)
    {
        _organizationReportRepo = organizationReportRepo;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<OrganizationReportApplicationDataFileStorageResponse> GetOrganizationReportApplicationDataAsync(
        Guid organizationId,
        Guid reportId,
        string reportFileId)
    {
        _logger.LogInformation(Constants.BypassFiltersEventId,
            "Generating download URL for application data - organization {organizationId}, report {reportId}",
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

        var downloadUrl = await _storageService.GetApplicationDataDownloadUrlAsync(report, reportFileId);

        return new OrganizationReportApplicationDataFileStorageResponse { DownloadUrl = downloadUrl };
    }
}
