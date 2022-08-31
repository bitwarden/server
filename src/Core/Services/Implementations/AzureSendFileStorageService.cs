using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class AzureSendFileStorageService : ISendFileStorageService
{
    public const string FilesContainerName = "sendfiles";
    private static readonly TimeSpan _downloadLinkLiveTime = TimeSpan.FromMinutes(1);
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<AzureSendFileStorageService> _logger;
    private BlobContainerClient _sendFilesContainerClient;

    public FileUploadType FileUploadType => FileUploadType.Azure;

    public static string SendIdFromBlobName(string blobName) => blobName.Split('/')[0];
    public static string BlobName(Send send, string fileId) => $"{send.Id}/{fileId}";

    public AzureSendFileStorageService(
        GlobalSettings globalSettings,
        ILogger<AzureSendFileStorageService> logger)
    {
        _blobServiceClient = new BlobServiceClient(globalSettings.Send.ConnectionString);
        _logger = logger;
    }

    public async Task UploadNewFileAsync(Stream stream, Send send, string fileId)
    {
        await InitAsync();

        var blobClient = _sendFilesContainerClient.GetBlobClient(BlobName(send, fileId));

        var metadata = new Dictionary<string, string>();
        if (send.UserId.HasValue)
        {
            metadata.Add("userId", send.UserId.Value.ToString());
        }
        else
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
        var blobClient = _sendFilesContainerClient.GetBlobClient(blobName);
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
        var blobClient = _sendFilesContainerClient.GetBlobClient(BlobName(send, fileId));
        var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTime.UtcNow.Add(_downloadLinkLiveTime));
        return sasUri.ToString();
    }

    public async Task<string> GetSendFileUploadUrlAsync(Send send, string fileId)
    {
        await InitAsync();
        var blobClient = _sendFilesContainerClient.GetBlobClient(BlobName(send, fileId));
        var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Create | BlobSasPermissions.Write, DateTime.UtcNow.Add(_downloadLinkLiveTime));
        return sasUri.ToString();
    }

    public async Task<(bool, long?)> ValidateFileAsync(Send send, string fileId, long expectedFileSize, long leeway)
    {
        await InitAsync();

        var blobClient = _sendFilesContainerClient.GetBlobClient(BlobName(send, fileId));

        try
        {
            var blobProperties = await blobClient.GetPropertiesAsync();
            var metadata = blobProperties.Value.Metadata;

            if (send.UserId.HasValue)
            {
                metadata["userId"] = send.UserId.Value.ToString();
            }
            else
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
            if (length < expectedFileSize - leeway || length > expectedFileSize + leeway)
            {
                return (false, length);
            }

            return (true, length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in ValidateFileAsync");
            return (false, null);
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
