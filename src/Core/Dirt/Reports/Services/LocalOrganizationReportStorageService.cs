using Bit.Core.Dirt.Entities;
using Bit.Core.Enums;
using Bit.Core.Settings;

namespace Bit.Core.Dirt.Reports.Services;

public class LocalOrganizationReportStorageService : IOrganizationReportStorageService
{
    private readonly string _baseDirPath;
    private readonly string _baseUrl;

    public FileUploadType FileUploadType => FileUploadType.Direct;

    public LocalOrganizationReportStorageService(GlobalSettings globalSettings)
    {
        _baseDirPath = globalSettings.OrganizationReport.BaseDirectory;
        _baseUrl = globalSettings.OrganizationReport.BaseUrl;
    }

    public Task<string> GetReportDataUploadUrlAsync(OrganizationReport report, string reportFileId)
        => Task.FromResult($"/reports/v2/organizations/{report.OrganizationId}/{report.Id}/file/report-data");

    public Task<string> GetReportDataDownloadUrlAsync(OrganizationReport report, string reportFileId)
    {
        InitDir();
        return Task.FromResult($"{_baseUrl}/{RelativePath(report, reportFileId, "report-data.json")}");
    }

    public async Task UploadReportDataAsync(OrganizationReport report, string reportFileId, Stream stream)
        => await WriteFileAsync(report, reportFileId, "report-data.json", stream);

    public Task DeleteReportFilesAsync(OrganizationReport report, string reportFileId)
    {
        var dirPath = Path.Combine(_baseDirPath, report.OrganizationId.ToString(),
            report.CreationDate.ToString("MM-dd-yyyy"), report.Id.ToString(), reportFileId);
        if (Directory.Exists(dirPath))
        {
            Directory.Delete(dirPath, true);
        }
        return Task.CompletedTask;
    }

    private async Task WriteFileAsync(OrganizationReport report, string reportFileId, string fileName, Stream stream)
    {
        InitDir();
        var path = Path.Combine(_baseDirPath, RelativePath(report, reportFileId, fileName));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fs = File.Create(path);
        stream.Seek(0, SeekOrigin.Begin);
        await stream.CopyToAsync(fs);
    }

    private static string RelativePath(OrganizationReport report, string reportFileId, string fileName)
    {
        var date = report.CreationDate.ToString("MM-dd-yyyy");
        return Path.Combine(report.OrganizationId.ToString(), date, report.Id.ToString(),
            reportFileId, fileName);
    }

    private void InitDir()
    {
        if (!Directory.Exists(_baseDirPath))
        {
            Directory.CreateDirectory(_baseDirPath);
        }
    }
}
