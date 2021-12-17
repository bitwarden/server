using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Settings;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;

namespace Bit.Core.Services
{
    public class AzureAttachmentStorageService : IAttachmentStorageService
    {
        public FileUploadType FileUploadType => FileUploadType.Azure;
        public const string EventGridEnabledContainerName = "attachments-v2";
        private const string _defaultContainerName = "attachments";
        private readonly static string[] _attachmentContainerName = { "attachments", "attachments-v2" };
        private static readonly TimeSpan blobLinkLiveTime = TimeSpan.FromMinutes(1);
        private readonly CloudBlobClient _blobClient;
        private readonly Dictionary<string, CloudBlobContainer> _attachmentContainers = new Dictionary<string, CloudBlobContainer>();
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
            GlobalSettings globalSettings)
        {
            var storageAccount = CloudStorageAccount.Parse(globalSettings.Attachment.ConnectionString);
            _blobClient = storageAccount.CreateCloudBlobClient();
        }

        public async Task<string> GetAttachmentDownloadUrlAsync(Cipher cipher, CipherAttachment.MetaData attachmentData)
        {
            await InitAsync(attachmentData.ContainerName);
            var blob = _attachmentContainers[attachmentData.ContainerName].GetBlockBlobReference(BlobName(cipher.Id, attachmentData));
            var accessPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.Add(blobLinkLiveTime),
                Permissions = SharedAccessBlobPermissions.Read
            };

            return blob.Uri + blob.GetSharedAccessSignature(accessPolicy);
        }

        public async Task<string> GetAttachmentUploadUrlAsync(Cipher cipher, CipherAttachment.MetaData attachmentData)
        {
            await InitAsync(EventGridEnabledContainerName);
            var blob = _attachmentContainers[EventGridEnabledContainerName].GetBlockBlobReference(BlobName(cipher.Id, attachmentData));
            attachmentData.ContainerName = EventGridEnabledContainerName;
            var accessPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.Add(blobLinkLiveTime),
                Permissions = SharedAccessBlobPermissions.Create | SharedAccessBlobPermissions.Write,
            };

            return blob.Uri + blob.GetSharedAccessSignature(accessPolicy);
        }

        public async Task UploadNewAttachmentAsync(Stream stream, Cipher cipher, CipherAttachment.MetaData attachmentData)
        {
            attachmentData.ContainerName = _defaultContainerName;
            await InitAsync(_defaultContainerName);
            var blob = _attachmentContainers[_defaultContainerName].GetBlockBlobReference(BlobName(cipher.Id, attachmentData));
            blob.Metadata.Add("cipherId", cipher.Id.ToString());
            if (cipher.UserId.HasValue)
            {
                blob.Metadata.Add("userId", cipher.UserId.Value.ToString());
            }
            else
            {
                blob.Metadata.Add("organizationId", cipher.OrganizationId.Value.ToString());
            }
            blob.Properties.ContentDisposition = $"attachment; filename=\"{attachmentData.AttachmentId}\"";
            await blob.UploadFromStreamAsync(stream);
        }

        public async Task UploadShareAttachmentAsync(Stream stream, Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData)
        {
            attachmentData.ContainerName = _defaultContainerName;
            await InitAsync(_defaultContainerName);
            var blob = _attachmentContainers[_defaultContainerName].GetBlockBlobReference(
                BlobName(cipherId, attachmentData, organizationId, temp: true));
            blob.Metadata.Add("cipherId", cipherId.ToString());
            blob.Metadata.Add("organizationId", organizationId.ToString());
            blob.Properties.ContentDisposition = $"attachment; filename=\"{attachmentData.AttachmentId}\"";
            await blob.UploadFromStreamAsync(stream);
        }

        public async Task StartShareAttachmentAsync(Guid cipherId, Guid organizationId, CipherAttachment.MetaData data)
        {
            await InitAsync(data.ContainerName);
            var source = _attachmentContainers[data.ContainerName].GetBlockBlobReference(
                    BlobName(cipherId, data, organizationId, temp: true));
            if (!(await source.ExistsAsync()))
            {
                return;
            }

            await InitAsync(_defaultContainerName);
            var dest = _attachmentContainers[_defaultContainerName].GetBlockBlobReference(BlobName(cipherId, data));
            if (!(await dest.ExistsAsync()))
            {
                return;
            }

            var original = _attachmentContainers[_defaultContainerName].GetBlockBlobReference(
                BlobName(cipherId, data, temp: true));
            await original.DeleteIfExistsAsync();
            await original.StartCopyAsync(dest);

            await dest.DeleteIfExistsAsync();
            await dest.StartCopyAsync(source);
        }

        public async Task RollbackShareAttachmentAsync(Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData, string originalContainer)
        {
            await InitAsync(attachmentData.ContainerName);
            var source = _attachmentContainers[attachmentData.ContainerName].GetBlockBlobReference(
                BlobName(cipherId, attachmentData, organizationId, temp: true));
            await source.DeleteIfExistsAsync();

            await InitAsync(originalContainer);
            var original = _attachmentContainers[originalContainer].GetBlockBlobReference(
                BlobName(cipherId, attachmentData, temp: true));
            if (!(await original.ExistsAsync()))
            {
                return;
            }

            var dest = _attachmentContainers[originalContainer].GetBlockBlobReference(
                BlobName(cipherId, attachmentData));
            await dest.DeleteIfExistsAsync();
            await dest.StartCopyAsync(original);
            await original.DeleteIfExistsAsync();
        }

        public async Task DeleteAttachmentAsync(Guid cipherId, CipherAttachment.MetaData attachmentData)
        {
            await InitAsync(attachmentData.ContainerName);
            var blob = _attachmentContainers[attachmentData.ContainerName].GetBlockBlobReference(
                BlobName(cipherId, attachmentData));
            await blob.DeleteIfExistsAsync();
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

            var blob = _attachmentContainers[attachmentData.ContainerName].GetBlockBlobReference(BlobName(cipher.Id, attachmentData));

            if (!blob.Exists())
            {
                return (false, null);
            }

            blob.FetchAttributes();

            blob.Metadata["cipherId"] = cipher.Id.ToString();
            if (cipher.UserId.HasValue)
            {
                blob.Metadata["userId"] = cipher.UserId.Value.ToString();
            }
            else
            {
                blob.Metadata["organizationId"] = cipher.OrganizationId.Value.ToString();
            }
            blob.Properties.ContentDisposition = $"attachment; filename=\"{attachmentData.AttachmentId}\"";
            blob.SetMetadata();
            blob.SetProperties();

            var length = blob.Properties.Length;
            if (length < attachmentData.Size - leeway || length > attachmentData.Size + leeway)
            {
                return (false, length);
            }

            return (true, length);
        }

        private async Task DeleteAttachmentsForPathAsync(string path)
        {
            foreach (var container in _attachmentContainerName)
            {
                await InitAsync(container);
                var segment = await _attachmentContainers[container].ListBlobsSegmentedAsync(path, true, BlobListingDetails.None, 100, null, null, null);

                while (true)
                {
                    foreach (var blob in segment.Results)
                    {
                        if (blob is CloudBlockBlob blockBlob)
                        {
                            await blockBlob.DeleteIfExistsAsync();
                        }
                    }

                    if (segment.ContinuationToken == null)
                    {
                        break;
                    }

                    segment = await _attachmentContainers[container].ListBlobsSegmentedAsync(segment.ContinuationToken);
                }
            }
        }

        private async Task InitAsync(string containerName)
        {
            if (!_attachmentContainers.ContainsKey(containerName) || _attachmentContainers[containerName] == null)
            {
                _attachmentContainers[containerName] = _blobClient.GetContainerReference(containerName);
                if (containerName == "attachments")
                {
                    await _attachmentContainers[containerName].CreateIfNotExistsAsync(BlobContainerPublicAccessType.Blob, null, null);
                }
                else
                {
                    await _attachmentContainers[containerName].CreateIfNotExistsAsync(BlobContainerPublicAccessType.Off, null, null);
                }
            }
        }
    }
}
