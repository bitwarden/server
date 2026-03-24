using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Enums;

namespace Bit.Core.Dirt.Reports.Services;

public interface IOrganizationReportStorageService
{
    FileUploadType FileUploadType { get; }

    Task<string> GetReportFileUploadUrlAsync(OrganizationReport report, ReportFile fileData);

    Task<string> GetReportDataDownloadUrlAsync(OrganizationReport report, ReportFile fileData);

    Task UploadReportDataAsync(OrganizationReport report, ReportFile fileData, Stream stream);

    Task<(bool valid, long length)> ValidateFileAsync(OrganizationReport report, ReportFile fileData, long minimum, long maximum);

    Task DeleteReportFilesAsync(OrganizationReport report, string reportFileId);

    /// <summary>
    /// Validates a time-limited download token and extracts the report ID and file ID.
    /// Only used by local/self-hosted storage implementations.
    /// </summary>
    (Guid reportId, string fileId) ParseReportDownloadToken(string token);

    /// <summary>
    /// Opens a read stream for the report file on disk.
    /// Only used by local/self-hosted storage implementations.
    /// </summary>
    Task<Stream?> GetReportReadStreamAsync(OrganizationReport report, ReportFile fileData);
}
