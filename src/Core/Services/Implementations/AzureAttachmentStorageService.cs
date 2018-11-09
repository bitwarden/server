using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;
using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public class AzureAttachmentStorageService : IAttachmentStorageService
    {
        private const string AttchmentContainerName = "attachments";

        private readonly CloudBlobClient _blobClient;
        private CloudBlobContainer _attachmentsContainer;

        public AzureAttachmentStorageService(
            GlobalSettings globalSettings)
        {
            var storageAccount = CloudStorageAccount.Parse(globalSettings.Attachment.ConnectionString);
            _blobClient = storageAccount.CreateCloudBlobClient();
        }

        public async Task UploadNewAttachmentAsync(Stream stream, Cipher cipher, string attachmentId)
        {
            await InitAsync();
            var blob = _attachmentsContainer.GetBlockBlobReference($"{cipher.Id}/{attachmentId}");
            blob.Metadata.Add("cipherId", cipher.Id.ToString());
            if(cipher.UserId.HasValue)
            {
                blob.Metadata.Add("userId", cipher.UserId.Value.ToString());
            }
            else
            {
                blob.Metadata.Add("organizationId", cipher.OrganizationId.Value.ToString());
            }
            blob.Properties.ContentDisposition = $"attachment; filename=\"{attachmentId}\"";
            await blob.UploadFromStreamAsync(stream);
        }

        public async Task UploadShareAttachmentAsync(Stream stream, Guid cipherId, Guid organizationId, string attachmentId)
        {
            await InitAsync();
            var blob = _attachmentsContainer.GetBlockBlobReference($"temp/{cipherId}/{organizationId}/{attachmentId}");
            blob.Metadata.Add("cipherId", cipherId.ToString());
            blob.Metadata.Add("organizationId", organizationId.ToString());
            blob.Properties.ContentDisposition = $"attachment; filename=\"{attachmentId}\"";
            await blob.UploadFromStreamAsync(stream);
        }

        public async Task StartShareAttachmentAsync(Guid cipherId, Guid organizationId, string attachmentId)
        {
            await InitAsync();
            var source = _attachmentsContainer.GetBlockBlobReference($"temp/{cipherId}/{organizationId}/{attachmentId}");
            if(!(await source.ExistsAsync()))
            {
                return;
            }

            var dest = _attachmentsContainer.GetBlockBlobReference($"{cipherId}/{attachmentId}");
            if(!(await dest.ExistsAsync()))
            {
                return;
            }

            var original = _attachmentsContainer.GetBlockBlobReference($"temp/{cipherId}/{attachmentId}");
            await original.DeleteIfExistsAsync();
            await original.StartCopyAsync(dest);

            await dest.DeleteIfExistsAsync();
            await dest.StartCopyAsync(source);
        }

        public async Task RollbackShareAttachmentAsync(Guid cipherId, Guid organizationId, string attachmentId)
        {
            await InitAsync();
            var source = _attachmentsContainer.GetBlockBlobReference($"temp/{cipherId}/{organizationId}/{attachmentId}");
            await source.DeleteIfExistsAsync();

            var original = _attachmentsContainer.GetBlockBlobReference($"temp/{cipherId}/{attachmentId}");
            if(!(await original.ExistsAsync()))
            {
                return;
            }

            var dest = _attachmentsContainer.GetBlockBlobReference($"{cipherId}/{attachmentId}");
            await dest.DeleteIfExistsAsync();
            await dest.StartCopyAsync(original);
            await original.DeleteIfExistsAsync();
        }

        public async Task DeleteAttachmentAsync(Guid cipherId, string attachmentId)
        {
            await InitAsync();
            var blobName = $"{cipherId}/{attachmentId}";
            var blob = _attachmentsContainer.GetBlockBlobReference(blobName);
            await blob.DeleteIfExistsAsync();
        }

        public async Task CleanupAsync(Guid cipherId)
        {
            await InitAsync();
            var segment = await _attachmentsContainer.ListBlobsSegmentedAsync($"temp/{cipherId}", true,
                BlobListingDetails.None, 100, null, null, null);

            while(true)
            {
                foreach(var blob in segment.Results)
                {
                    if(blob is CloudBlockBlob blockBlob)
                    {
                        await blockBlob.DeleteIfExistsAsync();
                    }
                }

                if(segment.ContinuationToken == null)
                {
                    break;
                }

                segment = await _attachmentsContainer.ListBlobsSegmentedAsync(segment.ContinuationToken);
            }
        }

        public async Task DeleteAttachmentsForCipherAsync(Guid cipherId)
        {
            await InitAsync();
            var segment = await _attachmentsContainer.ListBlobsSegmentedAsync(cipherId.ToString(), true,
                BlobListingDetails.None, 100, null, null, null);

            while(true)
            {
                foreach(var blob in segment.Results)
                {
                    if(blob is CloudBlockBlob blockBlob)
                    {
                        await blockBlob.DeleteIfExistsAsync();
                    }
                }

                if(segment.ContinuationToken == null)
                {
                    break;
                }

                segment = await _attachmentsContainer.ListBlobsSegmentedAsync(segment.ContinuationToken);
            }
        }

        public async Task DeleteAttachmentsForOrganizationAsync(Guid organizationId)
        {
            await InitAsync();
        }

        public async Task DeleteAttachmentsForUserAsync(Guid userId)
        {
            await InitAsync();
        }

        private async Task InitAsync()
        {
            if(_attachmentsContainer == null)
            {
                _attachmentsContainer = _blobClient.GetContainerReference(AttchmentContainerName);
                await _attachmentsContainer.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Blob, null, null);
            }
        }
    }
}
