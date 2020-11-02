using System.Threading.Tasks;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using System.IO;
using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public class AzureSendFileStorageService : ISendFileStorageService
    {
        private const string FilesContainerName = "sendfiles";

        private readonly CloudBlobClient _blobClient;
        private CloudBlobContainer _sendFilesContainer;

        public AzureSendFileStorageService(
            GlobalSettings globalSettings)
        {
            var storageAccount = CloudStorageAccount.Parse(globalSettings.Send.ConnectionString);
            _blobClient = storageAccount.CreateCloudBlobClient();
        }

        public async Task UploadNewFileAsync(Stream stream, Send send, string fileId)
        {
            await InitAsync();
            var blob = _sendFilesContainer.GetBlockBlobReference(fileId);
            if (send.UserId.HasValue)
            {
                blob.Metadata.Add("userId", send.UserId.Value.ToString());
            }
            else
            {
                blob.Metadata.Add("organizationId", send.OrganizationId.Value.ToString());
            }
            blob.Properties.ContentDisposition = $"attachment; filename=\"{fileId}\"";
            await blob.UploadFromStreamAsync(stream);
        }

        public async Task DeleteFileAsync(string fileId)
        {
            await InitAsync();
            var blob = _sendFilesContainer.GetBlockBlobReference(fileId);
            await blob.DeleteIfExistsAsync();
        }

        public async Task DeleteFilesForOrganizationAsync(Guid organizationId)
        {
            await InitAsync();
        }

        public async Task DeleteFilesForUserAsync(Guid userId)
        {
            await InitAsync();
        }

        private async Task InitAsync()
        {
            if (_sendFilesContainer == null)
            {
                _sendFilesContainer = _blobClient.GetContainerReference(FilesContainerName);
                await _sendFilesContainer.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Blob, null, null);
            }
        }
    }
}
