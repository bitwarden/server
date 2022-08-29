using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class AzureAttachmentStorageService : IAttachmentStorageService
{
    public FileUploadType FileUploadType => FileUploadType.Azure;
    public const string EventGridEnabledContainerName = "attachments-v2";
    private const string _defaultContainerName = "attachments";
    private readonly static string[] _attachmentContainerName = { "attachments", "attachments-v2" };
    private static readonly TimeSpan blobLinkLiveTime = TimeSpan.FromMinutes(1);
    private readonly BlobServiceClient _blobServiceClient;
    private readonly Dictionary<string, BlobContainerClient> _attachmentContainers = new Dictionary<string, BlobContainerClient>();
    private readonly ILogger<AzureAttachmentStorageService> _logger;

    private string BlobName(Guid cipherId, CipherAttachment.MetaData attachmentData, Guid? organizationId = null, bool temp = false) =>
        string.Concat(
            temp ? "temp/" : "",
            $"{cipherId}/",
            organizationId != null ? $"{organizationId.Value}/" : "",
            attachmentData.AttachmentId
        );

    public static (string cipherId, string organizationId, string attachmentId) IdentifiersFromBlobName(string blobName)
    {
        var parts = blobName.Split('/');
        switch (parts.Length)
        {
            case 4:
                return (parts[1], parts[2], parts[3]);
            case 3:
                if (parts[0] == "temp")
                {
                    return (parts[1], null, parts[2]);
                }
                else
                {
                    return (parts[0], parts[1], parts[2]);
                }
            case 2:
                return (parts[0], null, parts[1]);
            default:
                throw new Exception("Cannot determine cipher information from blob name");
        }
    }

    public AzureAttachmentStorageService(
        GlobalSettings globalSettings,
        ILogger<AzureAttachmentStorageService> logger)
    {
        _blobServiceClient = new BlobServiceClient(globalSettings.Attachment.ConnectionString);
        _logger = logger;
    }

    public async Task<string> GetAttachmentDownloadUrlAsync(Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        await InitAsync(attachmentData.ContainerName);
        var blobClient = _attachmentContainers[attachmentData.ContainerName].GetBlobClient(BlobName(cipher.Id, attachmentData));
        var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTime.UtcNow.Add(blobLinkLiveTime));
        return sasUri.ToString();
    }

    public async Task<string> GetAttachmentUploadUrlAsync(Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        await InitAsync(EventGridEnabledContainerName);
        var blobClient = _attachmentContainers[EventGridEnabledContainerName].GetBlobClient(BlobName(cipher.Id, attachmentData));
        attachmentData.ContainerName = EventGridEnabledContainerName;
        var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Create | BlobSasPermissions.Write, DateTime.UtcNow.Add(blobLinkLiveTime));
        return sasUri.ToString();
    }

    public async Task UploadNewAttachmentAsync(Stream stream, Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        attachmentData.ContainerName = _defaultContainerName;
        await InitAsync(_defaultContainerName);
        var blobClient = _attachmentContainers[_defaultContainerName].GetBlobClient(BlobName(cipher.Id, attachmentData));

        var metadata = new Dictionary<string, string>();
        metadata.Add("cipherId", cipher.Id.ToString());
        if (cipher.UserId.HasValue)
        {
            metadata.Add("userId", cipher.UserId.Value.ToString());
        }
        else
        {
            metadata.Add("organizationId", cipher.OrganizationId.Value.ToString());
        }

        var headers = new BlobHttpHeaders
        {
            ContentDisposition = $"attachment; filename=\"{attachmentData.AttachmentId}\""
        };
        await blobClient.UploadAsync(stream, new BlobUploadOptions { Metadata = metadata, HttpHeaders = headers });
    }

    public async Task UploadShareAttachmentAsync(Stream stream, Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData)
    {
        attachmentData.ContainerName = _defaultContainerName;
        await InitAsync(_defaultContainerName);
        var blobClient = _attachmentContainers[_defaultContainerName].GetBlobClient(
            BlobName(cipherId, attachmentData, organizationId, temp: true));

        var metadata = new Dictionary<string, string>();
        metadata.Add("cipherId", cipherId.ToString());
        metadata.Add("organizationId", organizationId.ToString());

        var headers = new BlobHttpHeaders
        {
            ContentDisposition = $"attachment; filename=\"{attachmentData.AttachmentId}\""
        };
        await blobClient.UploadAsync(stream, new BlobUploadOptions { Metadata = metadata, HttpHeaders = headers });
    }

    public async Task StartShareAttachmentAsync(Guid cipherId, Guid organizationId, CipherAttachment.MetaData data)
    {
        await InitAsync(data.ContainerName);
        var source = _attachmentContainers[data.ContainerName].GetBlobClient(
                BlobName(cipherId, data, organizationId, temp: true));
        if (!await source.ExistsAsync())
        {
            return;
        }

        await InitAsync(_defaultContainerName);
        var dest = _attachmentContainers[_defaultContainerName].GetBlobClient(BlobName(cipherId, data));
        if (!await dest.ExistsAsync())
        {
            return;
        }

        var original = _attachmentContainers[_defaultContainerName].GetBlobClient(
            BlobName(cipherId, data, temp: true));
        await original.DeleteIfExistsAsync();
        await original.StartCopyFromUriAsync(dest.Uri);

        await dest.DeleteIfExistsAsync();
        await dest.StartCopyFromUriAsync(source.Uri);
    }

    public async Task RollbackShareAttachmentAsync(Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData, string originalContainer)
    {
        await InitAsync(attachmentData.ContainerName);
        var source = _attachmentContainers[attachmentData.ContainerName].GetBlobClient(
            BlobName(cipherId, attachmentData, organizationId, temp: true));
        await source.DeleteIfExistsAsync();

        await InitAsync(originalContainer);
        var original = _attachmentContainers[originalContainer].GetBlobClient(
            BlobName(cipherId, attachmentData, temp: true));
        if (!await original.ExistsAsync())
        {
            return;
        }

        var dest = _attachmentContainers[originalContainer].GetBlobClient(
            BlobName(cipherId, attachmentData));
        await dest.DeleteIfExistsAsync();
        await dest.StartCopyFromUriAsync(original.Uri);
        await original.DeleteIfExistsAsync();
    }

    public async Task DeleteAttachmentAsync(Guid cipherId, CipherAttachment.MetaData attachmentData)
    {
        await InitAsync(attachmentData.ContainerName);
        var blobClient = _attachmentContainers[attachmentData.ContainerName].GetBlobClient(
            BlobName(cipherId, attachmentData));
        await blobClient.DeleteIfExistsAsync();
    }

    public async Task CleanupAsync(Guid cipherId) => await DeleteAttachmentsForPathAsync($"temp/{cipherId}");

    public async Task DeleteAttachmentsForCipherAsync(Guid cipherId) =>
        await DeleteAttachmentsForPathAsync(cipherId.ToString());

    public async Task DeleteAttachmentsForOrganizationAsync(Guid organizationId)
    {
        await InitAsync(_defaultContainerName);
    }

    public async Task DeleteAttachmentsForUserAsync(Guid userId)
    {
        await InitAsync(_defaultContainerName);
    }

    public async Task<(bool, long?)> ValidateFileAsync(Cipher cipher, CipherAttachment.MetaData attachmentData, long leeway)
    {
        await InitAsync(attachmentData.ContainerName);

        var blobClient = _attachmentContainers[attachmentData.ContainerName].GetBlobClient(BlobName(cipher.Id, attachmentData));

        try
        {
            var blobProperties = await blobClient.GetPropertiesAsync();

            var metadata = blobProperties.Value.Metadata;
            metadata["cipherId"] = cipher.Id.ToString();
            if (cipher.UserId.HasValue)
            {
                metadata["userId"] = cipher.UserId.Value.ToString();
            }
            else
            {
                metadata["organizationId"] = cipher.OrganizationId.Value.ToString();
            }
            await blobClient.SetMetadataAsync(metadata);

            var headers = new BlobHttpHeaders
            {
                ContentDisposition = $"attachment; filename=\"{attachmentData.AttachmentId}\""
            };
            await blobClient.SetHttpHeadersAsync(headers);

            var length = blobProperties.Value.ContentLength;
            if (length < attachmentData.Size - leeway || length > attachmentData.Size + leeway)
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

    private async Task DeleteAttachmentsForPathAsync(string path)
    {
        foreach (var container in _attachmentContainerName)
        {
            await InitAsync(container);
            var blobContainerClient = _attachmentContainers[container];

            var blobItems = blobContainerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: path);
            await foreach (var blobItem in blobItems)
            {
                BlobClient blobClient = blobContainerClient.GetBlobClient(blobItem.Name);
                await blobClient.DeleteIfExistsAsync();
            }
        }
    }

    private async Task InitAsync(string containerName)
    {
        if (!_attachmentContainers.ContainsKey(containerName) || _attachmentContainers[containerName] == null)
        {
            _attachmentContainers[containerName] = _blobServiceClient.GetBlobContainerClient(containerName);
            if (containerName == "attachments")
            {
                await _attachmentContainers[containerName].CreateIfNotExistsAsync(PublicAccessType.Blob, null, null);
            }
            else
            {
                await _attachmentContainers[containerName].CreateIfNotExistsAsync(PublicAccessType.None, null, null);
            }
        }
    }
}
