using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Enums;

namespace Bit.Core.Dirt.Reports.Services;

public interface IOrganizationReportStorageService
{
    FileUploadType FileUploadType { get; }

    Task<string> GetReportDataUploadUrlAsync(OrganizationReport report, OrganizationReportFileData fileData);

    Task<string> GetReportDataDownloadUrlAsync(OrganizationReport report, OrganizationReportFileData fileData);

    Task UploadReportDataAsync(OrganizationReport report, OrganizationReportFileData fileData, Stream stream);

    Task<(bool valid, long length)> ValidateFileAsync(OrganizationReport report, OrganizationReportFileData fileData, long minimum, long maximum);
}
