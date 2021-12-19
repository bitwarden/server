using System;
using System.IO;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Settings;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;

namespace Bit.Core.Services
{
    public class AzureSendFileStorageService : ISendFileStorageService
    {
        public const string FilesContainerName = "sendfiles";
        private static readonly TimeSpan _downloadLinkLiveTime = TimeSpan.FromMinutes(1);
        private readonly CloudBlobClient _blobClient;
        private CloudBlobContainer _sendFilesContainer;

        public FileUploadType FileUploadType => FileUploadType.Azure;

        public static string SendIdFromBlobName(string blobName) => blobName.Split('/')[0];
        public static string BlobName(Send send, string fileId) => $"{send.Id}/{fileId}";

        public AzureSendFileStorageService(
            GlobalSettings globalSettings)
        {
            var storageAccount = CloudStorageAccount.Parse(globalSettings.Send.ConnectionString);
            _blobClient = storageAccount.CreateCloudBlobClient();
        }

        public async Task UploadNewFileAsync(Stream stream, Send send, string fileId)
        {
            await InitAsync();
            var blob = _sendFilesContainer.GetBlockBlobReference(BlobName(send, fileId));
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

        public async Task DeleteFileAsync(Send send, string fileId) => await DeleteBlobAsync(BlobName(send, fileId));

        public async Task DeleteBlobAsync(string blobName)
        {
            await InitAsync();
            var blob = _sendFilesContainer.GetBlockBlobReference(blobName);
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

        public async Task<string> GetSendFileDownloadUrlAsync(Send send, string fileId)
        {
            await InitAsync();
            var blob = _sendFilesContainer.GetBlockBlobReference(BlobName(send, fileId));
            var accessPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.Add(_downloadLinkLiveTime),
                Permissions = SharedAccessBlobPermissions.Read,
            };

            return blob.Uri + blob.GetSharedAccessSignature(accessPolicy);
        }

        public async Task<string> GetSendFileUploadUrlAsync(Send send, string fileId)
        {
            await InitAsync();
            var blob = _sendFilesContainer.GetBlockBlobReference(BlobName(send, fileId));

            var accessPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.Add(_downloadLinkLiveTime),
                Permissions = SharedAccessBlobPermissions.Create | SharedAccessBlobPermissions.Write,
            };

            return blob.Uri + blob.GetSharedAccessSignature(accessPolicy);
        }

        public async Task<(bool, long?)> ValidateFileAsync(Send send, string fileId, long expectedFileSize, long leeway)
        {
            await InitAsync();

            var blob = _sendFilesContainer.GetBlockBlobReference(BlobName(send, fileId));

            if (!blob.Exists())
            {
                return (false, null);
            }

            blob.FetchAttributes();

            if (send.UserId.HasValue)
            {
                blob.Metadata["userId"] = send.UserId.Value.ToString();
            }
            else
            {
                blob.Metadata["organizationId"] = send.OrganizationId.Value.ToString();
            }
            blob.Properties.ContentDisposition = $"attachment; filename=\"{fileId}\"";
            blob.SetMetadata();
            blob.SetProperties();

            var length = blob.Properties.Length;
            if (length < expectedFileSize - leeway || length > expectedFileSize + leeway)
            {
                return (false, length);
            }

            return (true, length);
        }

        private async Task InitAsync()
        {
            if (_sendFilesContainer == null)
            {
                _sendFilesContainer = _blobClient.GetContainerReference(FilesContainerName);
                await _sendFilesContainer.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Off, null, null);
            }
        }
    }
}
