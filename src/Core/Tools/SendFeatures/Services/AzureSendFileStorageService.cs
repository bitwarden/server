using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Tools.Services;

public class AzureSendFileStorageService(
    GlobalSettings globalSettings,
    ISendRepository sendRepository,
    ILogger<AzureSendFileStorageService> logger) : ISendFileStorageService
{
    public const string FilesContainerName = "sendfiles";
    private static readonly TimeSpan _downloadLinkLiveTime = TimeSpan.FromMinutes(1);
    private readonly BlobServiceClient _blobServiceClient = new(globalSettings.Send.ConnectionString);
    private readonly ISendRepository _sendRepository = sendRepository;

    private readonly ILogger<AzureSendFileStorageService> _logger = logger;

    /*
     * When this file was made nullable, multiple instances of ! were introduced asserting that
     * _sendFilesContainerClient abd the blobClient it is used to construct are not null.
     *
     * See InitAsync() at end of file which is responsible for assigning value asynchronously ensuring
     * _sendFilesContainerClient and blobClient are not null.
     */
    private BlobContainerClient? _sendFilesContainerClient;

    public FileUploadType FileUploadType => FileUploadType.Azure;

    public static string SendIdFromBlobName(string blobName) => blobName.Split('/')[0];
    public static string BlobName(Send send, string fileId) => $"{send.Id}/{fileId}";

    public async Task UploadNewFileAsync(Stream stream, Send send, string fileId)
    {
        await InitAsync();

        var blobClient = _sendFilesContainerClient!.GetBlobClient(BlobName(send, fileId));

        var metadata = new Dictionary<string, string>();
        if (send.UserId.HasValue)
        {
            metadata.Add("userId", send.UserId.Value.ToString());
        }
        else if (send.OrganizationId.HasValue)
        {
            metadata.Add("organizationId", send.OrganizationId.Value.ToString());
        }

        var headers = new BlobHttpHeaders { ContentDisposition = $"attachment; filename=\"{fileId}\"" };

        await blobClient.UploadAsync(stream, new BlobUploadOptions { Metadata = metadata, HttpHeaders = headers });
    }

    public async Task DeleteFileAsync(Send send, string fileId) => await DeleteBlobAsync(BlobName(send, fileId));

    public async Task DeleteBlobAsync(string blobName)
    {
        await InitAsync();
        var blobClient = _sendFilesContainerClient!.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync();
    }

    public async Task DeleteFilesForOrganizationAsync(Guid organizationId)
    {
        await InitAsync();
        var sends = await _sendRepository.GetManyFileSendsByOrganizationIdAsync(organizationId);
        await DeleteBlobsForSendsAsync(sends);
    }

    public async Task DeleteFilesForUserAsync(Guid userId)
    {
        await InitAsync();
        var sends = await _sendRepository.GetManyFileSendsByUserIdAsync(userId);
        await DeleteBlobsForSendsAsync(sends);
    }

    public async Task<string> GetSendFileDownloadUrlAsync(Send send, string fileId)
    {
        await InitAsync();
        var blobClient = _sendFilesContainerClient!.GetBlobClient(BlobName(send, fileId));
        var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTime.UtcNow.Add(_downloadLinkLiveTime));
        return sasUri.ToString();
    }

    public async Task<string> GetSendFileUploadUrlAsync(Send send, string fileId)
    {
        await InitAsync();
        var blobClient = _sendFilesContainerClient!.GetBlobClient(BlobName(send, fileId));
        var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Create | BlobSasPermissions.Write,
            DateTime.UtcNow.Add(_downloadLinkLiveTime));
        return sasUri.ToString();
    }

    public async Task<(bool, long)> ValidateFileAsync(Send send, string fileId, long minimum, long maximum)
    {
        await InitAsync();

        var blobClient = _sendFilesContainerClient!.GetBlobClient(BlobName(send, fileId));

        try
        {
            var blobProperties = await blobClient.GetPropertiesAsync();
            var metadata = blobProperties.Value.Metadata;

            if (send.UserId.HasValue)
            {
                metadata["userId"] = send.UserId.Value.ToString();
            }
            else if (send.OrganizationId.HasValue)
            {
                metadata["organizationId"] = send.OrganizationId.Value.ToString();
            }

            await blobClient.SetMetadataAsync(metadata);

            var headers = new BlobHttpHeaders { ContentDisposition = $"attachment; filename=\"{fileId}\"" };
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

    private async Task DeleteBlobsForSendsAsync(ICollection<Send> fileSends)
    {
        var blobUris = new List<Uri>();

        foreach (var send in fileSends)
        {
            try
            {
                var data = send.Data != null
                    ? JsonSerializer.Deserialize<SendFileData>(send.Data)
                    : null;
                if (data?.Id != null)
                {
                    var blobClient = _sendFilesContainerClient!.GetBlobClient(BlobName(send, data.Id));
                    blobUris.Add(blobClient.Uri);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize Send {SendId} data; blob may be orphaned.", send.Id);
            }
        }

        if (blobUris.Count == 0)
        {
            return;
        }

        var blobBatchClient = _blobServiceClient.GetBlobBatchClient();

        foreach (var batch in blobUris.Chunk(256))
        {
            try
            {
                await blobBatchClient.DeleteBlobsAsync(batch);
            }
            catch (AggregateException ex)
            {
                _logger.LogError(ex,
                    "One or more blob deletions failed in a batch of {Count} blobs. The following URIs may be orphaned: {}",
                    batch.Length, string.Join<Uri>(", ", batch));
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex,
                    "Batch blob deletion request failed for {Count} blobs.The following URIs may be orphaned: {}",
                    batch.Length, string.Join<Uri>(", ", batch));
            }
        }
    }

    private async Task InitAsync()
    {
        if (_sendFilesContainerClient == null)
        {
            _sendFilesContainerClient = _blobServiceClient.GetBlobContainerClient(FilesContainerName);
            await _sendFilesContainerClient.CreateIfNotExistsAsync(PublicAccessType.None, null, null);
        }
    }
}
