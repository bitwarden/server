using System.Threading.Tasks;
using System.IO;
using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public class LocalAttachmentStorageService : IAttachmentStorageService
    {
        private readonly string _baseDirPath;
        private readonly string _baseTempDirPath;

        public LocalAttachmentStorageService(
            GlobalSettings globalSettings)
        {
            _baseDirPath = globalSettings.Attachment.BaseDirectory;
            _baseTempDirPath = $"{_baseDirPath}/temp";
        }

        public async Task UploadNewAttachmentAsync(Stream stream, Cipher cipher, string attachmentId)
        {
            await InitAsync();
            var cipherDirPath = $"{_baseDirPath}/{cipher.Id}";
            CreateDirectoryIfNotExists(cipherDirPath);

            using (var fs = File.Create($"{cipherDirPath}/{attachmentId}"))
            {
                stream.Seek(0, SeekOrigin.Begin);
                await stream.CopyToAsync(fs);
            }
        }

        public async Task UploadShareAttachmentAsync(Stream stream, Guid cipherId, Guid organizationId, string attachmentId)
        {
            await InitAsync();
            var tempCipherOrgDirPath = $"{_baseTempDirPath}/{cipherId}/{organizationId}";
            CreateDirectoryIfNotExists(tempCipherOrgDirPath);

            using (var fs = File.Create($"{tempCipherOrgDirPath}/{attachmentId}"))
            {
                stream.Seek(0, SeekOrigin.Begin);
                await stream.CopyToAsync(fs);
            }
        }

        public async Task StartShareAttachmentAsync(Guid cipherId, Guid organizationId, string attachmentId)
        {
            await InitAsync();
            var sourceFilePath = $"{_baseTempDirPath}/{cipherId}/{organizationId}/{attachmentId}";
            if (!File.Exists(sourceFilePath))
            {
                return;
            }

            var destFilePath = $"{_baseDirPath}/{cipherId}/{attachmentId}";
            if (!File.Exists(destFilePath))
            {
                return;
            }

            var originalFilePath = $"{_baseTempDirPath}/{cipherId}/{attachmentId}";
            DeleteFileIfExists(originalFilePath);

            File.Move(destFilePath, originalFilePath);
            DeleteFileIfExists(destFilePath);

            File.Move(sourceFilePath, destFilePath);
        }

        public async Task RollbackShareAttachmentAsync(Guid cipherId, Guid organizationId, string attachmentId)
        {
            await InitAsync();
            DeleteFileIfExists($"{_baseTempDirPath}/{cipherId}/{organizationId}/{attachmentId}");

            var originalFilePath = $"{_baseTempDirPath}/{cipherId}/{attachmentId}";
            if (!File.Exists(originalFilePath))
            {
                return;
            }

            var destFilePath = $"{_baseDirPath}/{cipherId}/{attachmentId}";
            DeleteFileIfExists(destFilePath);

            File.Move(originalFilePath, destFilePath);
            DeleteFileIfExists(originalFilePath);
        }

        public async Task DeleteAttachmentAsync(Guid cipherId, string attachmentId)
        {
            await InitAsync();
            DeleteFileIfExists($"{_baseDirPath}/{cipherId}/{attachmentId}");
        }

        public async Task CleanupAsync(Guid cipherId)
        {
            await InitAsync();
            DeleteDirectoryIfExists($"{_baseTempDirPath}/{cipherId}");
        }

        public async Task DeleteAttachmentsForCipherAsync(Guid cipherId)
        {
            await InitAsync();
            DeleteDirectoryIfExists($"{_baseDirPath}/{cipherId}");
        }

        public async Task DeleteAttachmentsForOrganizationAsync(Guid organizationId)
        {
            await InitAsync();
        }

        public async Task DeleteAttachmentsForUserAsync(Guid userId)
        {
            await InitAsync();
        }

        private void DeleteFileIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private void CreateDirectoryIfNotExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private Task InitAsync()
        {
            if (!Directory.Exists(_baseDirPath))
            {
                Directory.CreateDirectory(_baseDirPath);
            }

            if (!Directory.Exists(_baseTempDirPath))
            {
                Directory.CreateDirectory(_baseTempDirPath);
            }

            return Task.FromResult(0);
        }
    }
}
