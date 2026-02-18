using Bit.Core.Dirt.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Dirt.Reports.Services;

public interface IOrganizationReportStorageService
{
    FileUploadType FileUploadType { get; }

    Task<string> GetReportDataUploadUrlAsync(OrganizationReport report, string reportFileId);
    Task<string> GetSummaryDataUploadUrlAsync(OrganizationReport report, string reportFileId);
    Task<string> GetApplicationDataUploadUrlAsync(OrganizationReport report, string reportFileId);

    Task<string> GetReportDataDownloadUrlAsync(OrganizationReport report, string reportFileId);
    Task<string> GetSummaryDataDownloadUrlAsync(OrganizationReport report, string reportFileId);
    Task<string> GetApplicationDataDownloadUrlAsync(OrganizationReport report, string reportFileId);

    Task UploadReportDataAsync(OrganizationReport report, string reportFileId, Stream stream);
    Task UploadSummaryDataAsync(OrganizationReport report, string reportFileId, Stream stream);
    Task UploadApplicationDataAsync(OrganizationReport report, string reportFileId, Stream stream);

    Task DeleteReportFilesAsync(OrganizationReport report, string reportFileId);
}
