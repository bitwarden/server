using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;

public interface IUploadReceiveFileCommand
{
    /// <summary>
    /// Generates a file ID, records it in the Receive's data, and returns
    /// a time-limited upload URL along with the generated file ID.
    /// </summary>
    Task<(string Url, string FileId)> GetUploadUrlAsync(Receive receive);
}
