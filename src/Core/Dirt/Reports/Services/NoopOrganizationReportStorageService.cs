using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Enums;

namespace Bit.Core.Dirt.Reports.Services;

public class NoopOrganizationReportStorageService : IOrganizationReportStorageService
{
    public FileUploadType FileUploadType => FileUploadType.Direct;

    public Task<string> GetReportDataUploadUrlAsync(OrganizationReport report, OrganizationReportFileData fileData) => Task.FromResult(string.Empty);

    public Task<string> GetReportDataDownloadUrlAsync(OrganizationReport report, OrganizationReportFileData fileData) => Task.FromResult(string.Empty);

    public Task UploadReportDataAsync(OrganizationReport report, OrganizationReportFileData fileData, Stream stream) => Task.CompletedTask;

    public Task<(bool valid, long length)> ValidateFileAsync(OrganizationReport report, OrganizationReportFileData fileData, long minimum, long maximum) => Task.FromResult((true, 0L));
}
