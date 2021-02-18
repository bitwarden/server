using System.Threading.Tasks;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using System.IO;
using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using Bit.Core.Models.Data;

namespace Bit.Core.Services
{
    public class AzureAttachmentStorageService : IAttachmentStorageService
    {
        private const string _defaultContainerName = "attachments-v2";
        private readonly static string[] _attachmentContainerName = { "attachments", "attachments-v2" };
        private static readonly TimeSpan downloadLinkLiveTime = TimeSpan.FromMinutes(1);
        private readonly CloudBlobClient _blobClient;
        private readonly Dictionary<string, CloudBlobContainer> _attachmentContainers = new Dictionary<string, CloudBlobContainer>();

        public AzureAttachmentStorageService(
            GlobalSettings globalSettings)
        {
            var storageAccount = CloudStorageAccount.Parse(globalSettings.Attachment.ConnectionString);
            _blobClient = storageAccount.CreateCloudBlobClient();
        }

        public async Task<string> GetAttachmentDownloadUrlAsync(Cipher cipher, CipherAttachment.MetaData attachmentData)
        {
            await InitAsync(attachmentData.ContainerName);
            var blob = _attachmentContainers[attachmentData.ContainerName].GetBlockBlobReference($"{cipher.Id}/{attachmentData.AttachmentId}");
            var accessPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.Add(downloadLinkLiveTime),
                Permissions = SharedAccessBlobPermissions.Read
            };

            return blob.Uri + blob.GetSharedAccessSignature(accessPolicy);
        }

        public async Task UploadNewAttachmentAsync(Stream stream, Cipher cipher, CipherAttachment.MetaData attachment)
        {
            attachment.ContainerName = _defaultContainerName;
            await InitAsync(_defaultContainerName);
            var blob = _attachmentContainers[_defaultContainerName].GetBlockBlobReference($"{cipher.Id}/{attachment.AttachmentId}");
            blob.Metadata.Add("cipherId", cipher.Id.ToString());
            if (cipher.UserId.HasValue)
            {
                blob.Metadata.Add("userId", cipher.UserId.Value.ToString());
            }
            else
            {
                blob.Metadata.Add("organizationId", cipher.OrganizationId.Value.ToString());
            }
            blob.Properties.ContentDisposition = $"attachment; filename=\"{attachment.AttachmentId}\"";
            await blob.UploadFromStreamAsync(stream);
        }

        public async Task UploadShareAttachmentAsync(Stream stream, Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData)
        {
            attachmentData.ContainerName = _defaultContainerName;
            await InitAsync(_defaultContainerName);
            var blob = _attachmentContainers[_defaultContainerName].GetBlockBlobReference($"temp/{cipherId}/{organizationId}/{attachmentData.AttachmentId}");
            blob.Metadata.Add("cipherId", cipherId.ToString());
            blob.Metadata.Add("organizationId", organizationId.ToString());
            blob.Properties.ContentDisposition = $"attachment; filename=\"{attachmentData.AttachmentId}\"";
            await blob.UploadFromStreamAsync(stream);
        }

        public async Task StartShareAttachmentAsync(Guid cipherId, Guid organizationId, CipherAttachment.MetaData data)
        {
            await InitAsync(data.ContainerName);
            var source = _attachmentContainers[data.ContainerName].GetBlockBlobReference($"temp/{cipherId}/{organizationId}/{data.AttachmentId}");
            if (!(await source.ExistsAsync()))
            {
                return;
            }

            await InitAsync(_defaultContainerName);
            var dest = _attachmentContainers[_defaultContainerName].GetBlockBlobReference($"{cipherId}/{data.AttachmentId}");
            if (!(await dest.ExistsAsync()))
            {
                return;
            }

            var original = _attachmentContainers[_defaultContainerName].GetBlockBlobReference($"temp/{cipherId}/{data.AttachmentId}");
            await original.DeleteIfExistsAsync();
            await original.StartCopyAsync(dest);

            await dest.DeleteIfExistsAsync();
            await dest.StartCopyAsync(source);
        }

        public async Task RollbackShareAttachmentAsync(Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData, string originalContainer)
        {
            await InitAsync(attachmentData.ContainerName);
            var source = _attachmentContainers[attachmentData.ContainerName].GetBlockBlobReference($"temp/{cipherId}/{organizationId}/{attachmentData.AttachmentId}");
            await source.DeleteIfExistsAsync();

            await InitAsync(originalContainer);
            var original = _attachmentContainers[originalContainer].GetBlockBlobReference($"temp/{cipherId}/{attachmentData.AttachmentId}");
            if (!(await original.ExistsAsync()))
            {
                return;
            }

            var dest = _attachmentContainers[originalContainer].GetBlockBlobReference($"{cipherId}/{attachmentData.AttachmentId}");
            await dest.DeleteIfExistsAsync();
            await dest.StartCopyAsync(original);
            await original.DeleteIfExistsAsync();
        }

        public async Task DeleteAttachmentAsync(Guid cipherId, CipherAttachment.MetaData attachment)
        {
            await InitAsync(attachment.ContainerName);
            var blobName = $"{cipherId}/{attachment.AttachmentId}";
            var blob = _attachmentContainers[attachment.ContainerName].GetBlockBlobReference(blobName);
            await blob.DeleteIfExistsAsync();
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


        public async Task CleanupAsync(Guid cipherId) => await DeleteAttachmentsForPathAsync($"temp/{cipherId}");

        public async Task DeleteAttachmentsForCipherAsync(Guid cipherId) => await DeleteAttachmentsForPathAsync(cipherId.ToString());

        public async Task DeleteAttachmentsForOrganizationAsync(Guid organizationId)
        {
            await InitAsync(_defaultContainerName);
        }

        public async Task DeleteAttachmentsForUserAsync(Guid userId)
        {
            await InitAsync(_defaultContainerName);
        }

        private async Task InitAsync(string containerName)
        {
            if (!_attachmentContainers.ContainsKey(containerName) || _attachmentContainers[containerName] == null)
            {
                _attachmentContainers[containerName] = _blobClient.GetContainerReference(containerName);
                await _attachmentContainers[containerName].CreateIfNotExistsAsync(BlobContainerPublicAccessType.Off, null, null);
            }
        }
    }
}
