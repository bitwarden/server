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
}
