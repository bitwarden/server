using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;
using System;

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

        public async Task UploadNewAttachmentAsync(Stream stream, Guid cipherId, string attachmentId)
        {
            await UploadAttachmentAsync(stream, $"{cipherId}/{attachmentId}");
        }

        public async Task UploadShareAttachmentAsync(Stream stream, Guid cipherId, Guid organizationId, string attachmentId)
        {
            await UploadAttachmentAsync(stream, $"{cipherId}/share/{organizationId}/{attachmentId}");
        }

        public async Task StartShareAttachmentAsync(Guid cipherId, Guid organizationId, string attachmentId)
        {
            await InitAsync();
            var source = _attachmentsContainer.GetBlockBlobReference($"{cipherId}/share/{organizationId}/{attachmentId}");
            if(!await source.ExistsAsync())
            {
                return;
            }

            var dest = _attachmentsContainer.GetBlockBlobReference($"{cipherId}/{attachmentId}");
            if(!await dest.ExistsAsync())
            {
                return;
            }

            var original = _attachmentsContainer.GetBlockBlobReference($"{cipherId}/temp/{attachmentId}");
            await original.DeleteIfExistsAsync();
            await original.StartCopyAsync(dest);

            await dest.DeleteIfExistsAsync();
            await dest.StartCopyAsync(source);
        }

        public async Task CommitShareAttachmentAsync(Guid cipherId, Guid organizationId, string attachmentId)
        {
            await InitAsync();
            var source = _attachmentsContainer.GetBlockBlobReference($"{cipherId}/share/{organizationId}/{attachmentId}");
            var original = _attachmentsContainer.GetBlockBlobReference($"{cipherId}/temp/{attachmentId}");
            await original.DeleteIfExistsAsync();
            await source.DeleteIfExistsAsync();
        }

        public async Task RollbackShareAttachmentAsync(Guid cipherId, Guid organizationId, string attachmentId)
        {
            await InitAsync();
            var source = _attachmentsContainer.GetBlockBlobReference($"{cipherId}/share/{organizationId}/{attachmentId}");
            var dest = _attachmentsContainer.GetBlockBlobReference($"{cipherId}/{attachmentId}");
            var original = _attachmentsContainer.GetBlockBlobReference($"{cipherId}/temp/{attachmentId}");
            if(!await original.ExistsAsync())
            {
                return;
            }

            await dest.DeleteIfExistsAsync();
            await dest.StartCopyAsync(original);
            await original.DeleteIfExistsAsync();
            await source.DeleteIfExistsAsync();
        }

        public async Task DeleteAttachmentAsync(Guid cipherId, string attachmentId)
        {
            await InitAsync();
            var blobName = $"{cipherId}/{attachmentId}";
            var blob = _attachmentsContainer.GetBlockBlobReference(blobName);
            await blob.DeleteIfExistsAsync();
        }

        private async Task UploadAttachmentAsync(Stream stream, string blobName)
        {
            await InitAsync();
            var blob = _attachmentsContainer.GetBlockBlobReference(blobName);
            await blob.UploadFromStreamAsync(stream);
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
