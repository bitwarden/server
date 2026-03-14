using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Tools.Services;

public class AzureSendFileStorageService(
    GlobalSettings globalSettings,
    ILogger<AzureSendFileStorageService> logger) : ISendFileStorageService
{
    public const string FilesContainerName = "sendfiles";
    private static readonly TimeSpan _downloadLinkLiveTime = TimeSpan.FromMinutes(1);
    private readonly BlobServiceClient _blobServiceClient = new(globalSettings.Send.ConnectionString);
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

        var headers = new BlobHttpHeaders
        {
            ContentDisposition = $"attachment; filename=\"{fileId}\""
        };

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
    }

    public async Task DeleteFilesForUserAsync(Guid userId)
    {
        await InitAsync();
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
        var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Create | BlobSasPermissions.Write, DateTime.UtcNow.Add(_downloadLinkLiveTime));
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

            var headers = new BlobHttpHeaders
            {
                ContentDisposition = $"attachment; filename=\"{fileId}\""
            };
            await blobClient.SetHttpHeadersAsync(headers);

            var length = blobProperties.Value.ContentLength;
            var valid = minimum <= length || length <= maximum;

            return (valid, length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"A storage operation failed in {nameof(ValidateFileAsync)}");
            return (false, -1);
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
