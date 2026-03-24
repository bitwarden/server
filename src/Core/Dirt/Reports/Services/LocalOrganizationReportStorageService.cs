using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Settings;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.Dirt.Reports.Services;

public class LocalOrganizationReportStorageService : IOrganizationReportStorageService
{
    private readonly string _baseDirPath;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly string _apiBaseUrl;

    internal static readonly string ReportDownloadProtectorPurpose = "ReportDownload";
    private static readonly TimeSpan _downloadLinkLifetime = TimeSpan.FromMinutes(1);

    public FileUploadType FileUploadType => FileUploadType.Direct;

    public LocalOrganizationReportStorageService(
        GlobalSettings globalSettings,
        IDataProtectionProvider dataProtectionProvider)
    {
        _baseDirPath = globalSettings.OrganizationReport.BaseDirectory;
        _dataProtectionProvider = dataProtectionProvider;
        _apiBaseUrl = globalSettings.BaseServiceUri.Api;
    }

    public Task<string> GetReportFileUploadUrlAsync(OrganizationReport report, ReportFile fileData)
        => Task.FromResult($"/reports/organizations/{report.OrganizationId}/{report.Id}/file/report-data");

    public Task<string> GetReportDataDownloadUrlAsync(OrganizationReport report, ReportFile fileData)
    {
        InitDir();
        var protector = _dataProtectionProvider.CreateProtector(ReportDownloadProtectorPurpose);
        var timedProtector = protector.ToTimeLimitedDataProtector();
        var token = timedProtector.Protect(
            $"{report.Id}|{fileData.Id}",
            _downloadLinkLifetime);
        return Task.FromResult($"{_apiBaseUrl}/reports/download?token={Uri.EscapeDataString(token)}");
    }

    public (Guid reportId, string fileId) ParseReportDownloadToken(string token)
    {
        var protector = _dataProtectionProvider
            .CreateProtector(ReportDownloadProtectorPurpose)
            .ToTimeLimitedDataProtector();

        string payload;
        try
        {
            payload = protector.Unprotect(token);
        }
        catch
        {
            throw new NotFoundException();
        }

        var parts = payload.Split('|');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var reportId))
        {
            throw new NotFoundException();
        }

        return (reportId, parts[1]);
    }

    public Task<Stream?> GetReportReadStreamAsync(OrganizationReport report, ReportFile fileData)
    {
        var path = Path.Combine(_baseDirPath, RelativePath(report, fileData.Id!, fileData.FileName));
        EnsurePathWithinBaseDir(path);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(File.OpenRead(path));
    }

    public async Task UploadReportDataAsync(OrganizationReport report, ReportFile fileData, Stream stream)
        => await WriteFileAsync(report, fileData.Id!, fileData.FileName, stream);

    public Task<(bool valid, long length)> ValidateFileAsync(
        OrganizationReport report, ReportFile fileData, long minimum, long maximum)
    {
        var path = Path.Combine(_baseDirPath, RelativePath(report, fileData.Id!, fileData.FileName));
        EnsurePathWithinBaseDir(path);
        if (!File.Exists(path))
        {
            return Task.FromResult((false, -1L));
        }

        var length = new FileInfo(path).Length;
        var valid = minimum <= length && length <= maximum;
        return Task.FromResult((valid, length));
    }

    public Task DeleteReportFilesAsync(OrganizationReport report, string reportFileId)
    {
        var dirPath = Path.Combine(_baseDirPath, report.OrganizationId.ToString(),
            report.CreationDate.ToString("MM-dd-yyyy"), report.Id.ToString(), reportFileId);
        EnsurePathWithinBaseDir(dirPath);
        if (Directory.Exists(dirPath))
        {
            Directory.Delete(dirPath, true);
        }
        return Task.CompletedTask;
    }

    private async Task WriteFileAsync(OrganizationReport report, string fileId, string fileName, Stream stream)
    {
        InitDir();
        var path = Path.Combine(_baseDirPath, RelativePath(report, fileId, fileName));
        EnsurePathWithinBaseDir(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fs = File.Create(path);
        stream.Seek(0, SeekOrigin.Begin);
        await stream.CopyToAsync(fs);
    }

    private static string RelativePath(OrganizationReport report, string fileId, string fileName)
    {
        var date = report.CreationDate.ToString("MM-dd-yyyy");
        return Path.Combine(report.OrganizationId.ToString(), date, report.Id.ToString(),
            fileId, fileName);
    }

    private void EnsurePathWithinBaseDir(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var fullBaseDir = Path.GetFullPath(_baseDirPath + Path.DirectorySeparatorChar);
        if (!fullPath.StartsWith(fullBaseDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path traversal detected.");
        }
    }

    private void InitDir()
    {
        if (!Directory.Exists(_baseDirPath))
        {
            Directory.CreateDirectory(_baseDirPath);
        }
    }
}
