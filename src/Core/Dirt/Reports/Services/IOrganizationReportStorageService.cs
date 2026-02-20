using Bit.Core.Dirt.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Dirt.Reports.Services;

public interface IOrganizationReportStorageService
{
    FileUploadType FileUploadType { get; }

    Task<string> GetReportDataUploadUrlAsync(OrganizationReport report, string reportFileId);

    Task<string> GetReportDataDownloadUrlAsync(OrganizationReport report, string reportFileId);

    Task UploadReportDataAsync(OrganizationReport report, string reportFileId, Stream stream);

}
