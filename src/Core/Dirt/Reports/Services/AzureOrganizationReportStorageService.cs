using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Bit.Core.Dirt.Entities;
using Bit.Core.Enums;
using Bit.Core.Settings;

namespace Bit.Core.Dirt.Reports.Services;

public class AzureOrganizationReportStorageService : IOrganizationReportStorageService
{
    public const string ContainerName = "organization-reports";
    private static readonly TimeSpan _sasTokenLifetime = TimeSpan.FromMinutes(1);

    private readonly BlobServiceClient _blobServiceClient;
    private BlobContainerClient? _containerClient;

    public FileUploadType FileUploadType => FileUploadType.Azure;

    public AzureOrganizationReportStorageService(GlobalSettings globalSettings)
    {
        _blobServiceClient = new BlobServiceClient(globalSettings.OrganizationReport.ConnectionString);
    }

    public async Task<string> GetReportDataUploadUrlAsync(OrganizationReport report, string reportFileId)
    {
        await InitAsync();
        var blobClient = _containerClient!.GetBlobClient(BlobPath(report, reportFileId, "report-data.json"));
        return blobClient.GenerateSasUri(
            BlobSasPermissions.Create | BlobSasPermissions.Write,
            DateTime.UtcNow.Add(_sasTokenLifetime)).ToString();
    }

    public async Task<string> GetSummaryDataUploadUrlAsync(OrganizationReport report, string reportFileId)
    {
        await InitAsync();
        var blobClient = _containerClient!.GetBlobClient(BlobPath(report, reportFileId, "summary-data.json"));
        return blobClient.GenerateSasUri(
            BlobSasPermissions.Create | BlobSasPermissions.Write,
            DateTime.UtcNow.Add(_sasTokenLifetime)).ToString();
    }

    public async Task<string> GetApplicationDataUploadUrlAsync(OrganizationReport report, string reportFileId)
    {
        await InitAsync();
        var blobClient = _containerClient!.GetBlobClient(BlobPath(report, reportFileId, "application-data.json"));
        return blobClient.GenerateSasUri(
            BlobSasPermissions.Create | BlobSasPermissions.Write,
            DateTime.UtcNow.Add(_sasTokenLifetime)).ToString();
    }

    public async Task<string> GetReportDataDownloadUrlAsync(OrganizationReport report, string reportFileId)
    {
        await InitAsync();
        var blobClient = _containerClient!.GetBlobClient(BlobPath(report, reportFileId, "report-data.json"));
        return blobClient.GenerateSasUri(BlobSasPermissions.Read,
            DateTime.UtcNow.Add(_sasTokenLifetime)).ToString();
    }

    public async Task<string> GetSummaryDataDownloadUrlAsync(OrganizationReport report, string reportFileId)
    {
        await InitAsync();
        var blobClient = _containerClient!.GetBlobClient(BlobPath(report, reportFileId, "summary-data.json"));
        return blobClient.GenerateSasUri(BlobSasPermissions.Read,
            DateTime.UtcNow.Add(_sasTokenLifetime)).ToString();
    }

    public async Task<string> GetApplicationDataDownloadUrlAsync(OrganizationReport report, string reportFileId)
    {
        await InitAsync();
        var blobClient = _containerClient!.GetBlobClient(BlobPath(report, reportFileId, "application-data.json"));
        return blobClient.GenerateSasUri(BlobSasPermissions.Read,
            DateTime.UtcNow.Add(_sasTokenLifetime)).ToString();
    }

    public async Task UploadReportDataAsync(OrganizationReport report, string reportFileId, Stream stream)
    {
        await InitAsync();
        var blobClient = _containerClient!.GetBlobClient(BlobPath(report, reportFileId, "report-data.json"));
        await blobClient.UploadAsync(stream, overwrite: true);
    }

    public async Task UploadSummaryDataAsync(OrganizationReport report, string reportFileId, Stream stream)
    {
        await InitAsync();
        var blobClient = _containerClient!.GetBlobClient(BlobPath(report, reportFileId, "summary-data.json"));
        await blobClient.UploadAsync(stream, overwrite: true);
    }

    public async Task UploadApplicationDataAsync(OrganizationReport report, string reportFileId, Stream stream)
    {
        await InitAsync();
        var blobClient = _containerClient!.GetBlobClient(BlobPath(report, reportFileId, "application-data.json"));
        await blobClient.UploadAsync(stream, overwrite: true);
    }

    public async Task DeleteReportFilesAsync(OrganizationReport report, string reportFileId)
    {
        await InitAsync();
        var dataTypes = new[] { "report-data.json", "summary-data.json", "application-data.json" };
        foreach (var dataType in dataTypes)
        {
            var blobClient = _containerClient!.GetBlobClient(BlobPath(report, reportFileId, dataType));
            await blobClient.DeleteIfExistsAsync();
        }
    }

    private static string BlobPath(OrganizationReport report, string reportFileId, string fileName)
    {
        var date = report.CreationDate.ToString("MM-dd-yyyy");
        return $"{report.OrganizationId}/{date}/{report.Id}/{reportFileId}/{fileName}";
    }

    private async Task InitAsync()
    {
        if (_containerClient == null)
        {
            _containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
        }
    }
}
