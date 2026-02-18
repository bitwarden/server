using Bit.Core.Dirt.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Dirt.Reports.Services;

public class NoopOrganizationReportStorageService : IOrganizationReportStorageService
{
    public FileUploadType FileUploadType => FileUploadType.Direct;

    public Task<string> GetReportDataUploadUrlAsync(OrganizationReport report, string reportFileId) => Task.FromResult(string.Empty);
    public Task<string> GetSummaryDataUploadUrlAsync(OrganizationReport report, string reportFileId) => Task.FromResult(string.Empty);
    public Task<string> GetApplicationDataUploadUrlAsync(OrganizationReport report, string reportFileId) => Task.FromResult(string.Empty);

    public Task<string> GetReportDataDownloadUrlAsync(OrganizationReport report, string reportFileId) => Task.FromResult(string.Empty);
    public Task<string> GetSummaryDataDownloadUrlAsync(OrganizationReport report, string reportFileId) => Task.FromResult(string.Empty);
    public Task<string> GetApplicationDataDownloadUrlAsync(OrganizationReport report, string reportFileId) => Task.FromResult(string.Empty);

    public Task UploadReportDataAsync(OrganizationReport report, string reportFileId, Stream stream) => Task.CompletedTask;
    public Task UploadSummaryDataAsync(OrganizationReport report, string reportFileId, Stream stream) => Task.CompletedTask;
    public Task UploadApplicationDataAsync(OrganizationReport report, string reportFileId, Stream stream) => Task.CompletedTask;

    public Task DeleteReportFilesAsync(OrganizationReport report, string reportFileId) => Task.CompletedTask;
}
