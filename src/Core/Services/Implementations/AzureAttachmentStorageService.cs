using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;

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

        public async Task UploadAttachmentAsync(Stream stream, string name)
        {
            await InitAsync();
            var blob = _attachmentsContainer.GetBlockBlobReference(name);
            await blob.UploadFromStreamAsync(stream);
        }

        public async Task DeleteAttachmentAsync(string name)
        {
            await InitAsync();
            var blob = _attachmentsContainer.GetBlockBlobReference(name);
            await blob.DeleteIfExistsAsync();
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
