using System.IO;
using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public class NoopAttachmentStorageService : IAttachmentStorageService
    {
        public Task DeleteAttachmentAsync(string name)
        {
            return Task.FromResult(0);
        }

        public Task UploadAttachmentAsync(Stream stream, string name)
        {
            return Task.FromResult(0);
        }
    }
}
