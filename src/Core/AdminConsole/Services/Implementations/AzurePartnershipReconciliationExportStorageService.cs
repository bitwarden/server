using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.Services.Implementations;

public class AzurePartnershipReconciliationExportStorageService : IPartnershipReconciliationExportStorageService
{
    public const string ContainerName = "partnership-reconciliation-exports";
    public const string ContentType = "application/x-ndjson";

    private readonly BlobServiceClient _blobServiceClient;
    private readonly TimeSpan _downloadUrlLifetime;
    private readonly TimeProvider _timeProvider;
    private BlobContainerClient? _containerClient;

    public AzurePartnershipReconciliationExportStorageService(GlobalSettings globalSettings, TimeProvider timeProvider)
    {
        _blobServiceClient = new BlobServiceClient(globalSettings.Partnerships.ReconciliationExportConnectionString);
        _downloadUrlLifetime = globalSettings.Partnerships.ReconciliationExportDownloadUrlLifetime;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// For unit tests: uses <paramref name="containerClient"/> as is, so no call is made to create the container.
    /// </summary>
    internal AzurePartnershipReconciliationExportStorageService(
        BlobContainerClient containerClient, TimeSpan downloadUrlLifetime, TimeProvider timeProvider)
    {
        _blobServiceClient = null!;
        _containerClient = containerClient;
        _downloadUrlLifetime = downloadUrlLifetime;
        _timeProvider = timeProvider;
    }

    public async Task<Stream> OpenWriteAsync(string blobName)
    {
        var blobClient = (await GetContainerClientAsync()).GetBlobClient(blobName);
        return await blobClient.OpenWriteAsync(overwrite: true, new BlobOpenWriteOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = ContentType,
                ContentDisposition = $"attachment; filename=\"{Path.GetFileName(blobName)}\"",
            },
        });
    }

    public async Task<string?> GetDownloadUrlAsync(string blobName)
    {
        var blobClient = (await GetContainerClientAsync()).GetBlobClient(blobName);
        return blobClient.GenerateSasUri(BlobSasPermissions.Read,
            _timeProvider.GetUtcNow().Add(_downloadUrlLifetime)).ToString();
    }

    public async Task DeleteAsync(string blobName)
    {
        var blobClient = (await GetContainerClientAsync()).GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync();
    }

    private async Task<BlobContainerClient> GetContainerClientAsync()
    {
        if (_containerClient == null)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
            _containerClient = containerClient;
        }

        return _containerClient;
    }
}
