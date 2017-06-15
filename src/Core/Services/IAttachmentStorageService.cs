using System.IO;
using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public interface IAttachmentStorageService
    {
        Task UploadAttachmentAsync(Stream stream, string name);
        Task DeleteAttachmentAsync(string name);
    }
}
