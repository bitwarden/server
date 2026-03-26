using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Tools.Services;

public class AzureReceiveFileStorageService : IReceiveFileStorageService
{
    public const string FilesContainerName = "receivefiles";
    private static readonly TimeSpan _downloadLinkLiveTime = TimeSpan.FromMinutes(1);
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<AzureReceiveFileStorageService> _logger;
    private BlobContainerClient? _receiveFilesContainerClient;
    private readonly IReceiveRepository _receiveRepository;
    private readonly IPushNotificationService _pushNotificationService;

    public FileUploadType FileUploadType => FileUploadType.Azure;

    public static string ReceiveIdFromBlobName(string blobName) => blobName.Split('/')[0];
    public static string BlobName(Receive receive, string fileId) => $"{receive.Id}/{fileId}";

    public AzureReceiveFileStorageService(
        GlobalSettings globalSettings,
        ILogger<AzureReceiveFileStorageService> logger,
        IReceiveRepository receiveRepository,
        IPushNotificationService pushNotificationService)
    {
        // TODO: coordinate with appropriate team to ensure Receives have dedicated storage and update the line below
        _blobServiceClient = new BlobServiceClient(globalSettings.Send.ConnectionString);
        _logger = logger;
        _receiveRepository = receiveRepository;
        _pushNotificationService = pushNotificationService;
    }

    public async Task UploadNewFileAsync(Stream stream, Receive receive, string fileId)
    {
        await InitAsync();

        var blobClient = _receiveFilesContainerClient!.GetBlobClient(BlobName(receive, fileId));

        var metadata = new Dictionary<string, string>();
        metadata.Add("userId", receive.UserId.ToString());

        var headers = new BlobHttpHeaders
        {
            ContentDisposition = $"attachment; filename=\"{fileId}\""
        };

        await blobClient.UploadAsync(stream, new BlobUploadOptions { Metadata = metadata, HttpHeaders = headers });

        receive.UploadCount++;
        await _receiveRepository.ReplaceAsync(receive);

        // TODO: investigate if this belongs here, if it does adapt the existing Send method to support Receive type
        // await _pushNotificationService.PushSyncSendUpdateAsync(receive);
    }

    public async Task DeleteFileAsync(Receive receive, string fileId) => await DeleteBlobAsync(BlobName(receive, fileId));

    public async Task DeleteBlobAsync(string blobName)
    {
        await InitAsync();

        var blobClient = _receiveFilesContainerClient!.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync();
    }

    public async Task<string> GetReceiveFileDownloadUrlAsync(Receive receive, string fileId)
    {
        await InitAsync();

        var blobClient = _receiveFilesContainerClient!.GetBlobClient(BlobName(receive, fileId));
        var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTime.UtcNow.Add(_downloadLinkLiveTime));
        return sasUri.ToString();
    }

    public async Task<string> GetReceiveFileUploadUrlAsync(Receive receive, string fileId)
    {
        await InitAsync();

        var blobClient = _receiveFilesContainerClient!.GetBlobClient(BlobName(receive, fileId));
        var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Create | BlobSasPermissions.Write, DateTime.UtcNow.Add(_downloadLinkLiveTime));
        return sasUri.ToString();
    }

    public async Task<(bool, long)> ValidateFileAsync(Receive receive, string fileId, long minimum, long maximum)
    {
        await InitAsync();

        var blobClient = _receiveFilesContainerClient!.GetBlobClient(BlobName(receive, fileId));

        try
        {
            var blobProperties = await blobClient.GetPropertiesAsync();
            var metadata = blobProperties.Value.Metadata;
            metadata["userId"] = receive.UserId.ToString();

            await blobClient.SetMetadataAsync(metadata);

            var headers = new BlobHttpHeaders
            {
                ContentDisposition = $"attachment; filename=\"{fileId}\""
            };
            await blobClient.SetHttpHeadersAsync(headers);

            var length = blobProperties.Value.ContentLength;
            var valid = minimum <= length && length <= maximum;

            return (valid, length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"A storage operation failed in {nameof(ValidateFileAsync)}");
            return (false, -1);
        }
    }

    private async Task InitAsync()
    {
        if (_receiveFilesContainerClient == null)
        {
            _receiveFilesContainerClient = _blobServiceClient.GetBlobContainerClient(FilesContainerName);
            await _receiveFilesContainerClient.CreateIfNotExistsAsync(PublicAccessType.None, null, null);
        }
    }
}
