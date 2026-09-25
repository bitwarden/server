using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Enums;

namespace Bit.Core.Dirt.Reports.Services;

public class NoopOrganizationReportStorageService : IOrganizationReportStorageService
{
    public FileUploadType FileUploadType => FileUploadType.Direct;

    public Task<string> GetReportFileUploadUrlAsync(OrganizationReport report, ReportFile fileData) => Task.FromResult(string.Empty);

    public Task<string> GetReportDataDownloadUrlAsync(OrganizationReport report, ReportFile fileData) => Task.FromResult(string.Empty);

    public Task UploadReportDataAsync(OrganizationReport report, ReportFile fileData, Stream stream) => Task.CompletedTask;

    public Task<(bool valid, long length)> ValidateFileAsync(OrganizationReport report, ReportFile fileData, long minimum, long maximum) => Task.FromResult((true, fileData.Size));

    public Task DeleteReportFilesAsync(OrganizationReport report, string reportFileId) => Task.CompletedTask;

    public Task<Stream?> GetReportReadStreamAsync(OrganizationReport report, ReportFile fileData) => Task.FromResult<Stream?>(null);
}
