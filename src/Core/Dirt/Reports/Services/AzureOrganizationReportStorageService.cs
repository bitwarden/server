using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Reports.Services;

public class AzureOrganizationReportStorageService : IOrganizationReportStorageService
{
    public const string ContainerName = "organization-reports";
    private static readonly TimeSpan _sasTokenLifetime = TimeSpan.FromMinutes(1);

    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<AzureOrganizationReportStorageService> _logger;
    private BlobContainerClient? _containerClient;

    public FileUploadType FileUploadType => FileUploadType.Azure;

    public AzureOrganizationReportStorageService(
        GlobalSettings globalSettings,
        ILogger<AzureOrganizationReportStorageService> logger)
    {
        _blobServiceClient = new BlobServiceClient(globalSettings.OrganizationReport.ConnectionString);
        _logger = logger;
    }

    public async Task<string> GetReportDataUploadUrlAsync(OrganizationReport report, OrganizationReportFileData fileData)
    {
        await InitAsync();
        var blobClient = _containerClient!.GetBlobClient(BlobPath(report, fileData.Id!, fileData.FileName));
        return blobClient.GenerateSasUri(
            BlobSasPermissions.Create | BlobSasPermissions.Write,
            DateTime.UtcNow.Add(_sasTokenLifetime)).ToString();
    }

    public async Task<string> GetReportDataDownloadUrlAsync(OrganizationReport report, OrganizationReportFileData fileData)
    {
        await InitAsync();
        var blobClient = _containerClient!.GetBlobClient(BlobPath(report, fileData.Id!, fileData.FileName));
        return blobClient.GenerateSasUri(BlobSasPermissions.Read,
            DateTime.UtcNow.Add(_sasTokenLifetime)).ToString();
    }

    public async Task UploadReportDataAsync(OrganizationReport report, OrganizationReportFileData fileData, Stream stream)
    {
        await InitAsync();
        var blobClient = _containerClient!.GetBlobClient(BlobPath(report, fileData.Id!, fileData.FileName));
        await blobClient.UploadAsync(stream, overwrite: true);
    }

    public async Task<(bool valid, long length)> ValidateFileAsync(
        OrganizationReport report, OrganizationReportFileData fileData, long minimum, long maximum)
    {
        await InitAsync();

        var blobClient = _containerClient!.GetBlobClient(BlobPath(report, fileData.Id!, fileData.FileName));

        try
        {
            var blobProperties = await blobClient.GetPropertiesAsync();
            var metadata = blobProperties.Value.Metadata;
            metadata["organizationId"] = report.OrganizationId.ToString();
            await blobClient.SetMetadataAsync(metadata);

            var headers = new BlobHttpHeaders
            {
                ContentDisposition = $"attachment; filename=\"{fileData.FileName}\""
            };
            await blobClient.SetHttpHeadersAsync(headers);

            var length = blobProperties.Value.ContentLength;
            var valid = minimum <= length && length <= maximum;

            return (valid, length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "A storage operation failed in {MethodName}", nameof(ValidateFileAsync));
            return (false, -1);
        }
    }

    private static string BlobPath(OrganizationReport report, string fileId, string fileName)
    {
        var date = report.CreationDate.ToString("MM-dd-yyyy");
        return $"{report.OrganizationId}/{date}/{report.Id}/{fileId}/{fileName}";
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
