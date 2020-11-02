using System.Threading.Tasks;
using System.IO;
using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public class LocalSendStorageService : ISendFileStorageService
    {
        private readonly string _baseDirPath;

        public LocalSendStorageService(
            GlobalSettings globalSettings)
        {
            _baseDirPath = globalSettings.Send.BaseDirectory;
        }

        public async Task UploadNewFileAsync(Stream stream, Send send, string fileId)
        {
            await InitAsync();
            using (var fs = File.Create($"{_baseDirPath}/{fileId}"))
            {
                stream.Seek(0, SeekOrigin.Begin);
                await stream.CopyToAsync(fs);
            }
        }

        public async Task DeleteFileAsync(string fileId)
        {
            await InitAsync();
            DeleteFileIfExists($"{_baseDirPath}/{fileId}");
        }

        public async Task DeleteFilesForOrganizationAsync(Guid organizationId)
        {
            await InitAsync();
        }

        public async Task DeleteFilesForUserAsync(Guid userId)
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

        private Task InitAsync()
        {
            if (!Directory.Exists(_baseDirPath))
            {
                Directory.CreateDirectory(_baseDirPath);
            }

            return Task.FromResult(0);
        }
    }
}
