using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;

public interface IUploadReceiveFileCommand
{
    /// <summary>
    /// Generates a file ID and returns a time-limited upload URL for the given Receive.
    /// </summary>
    Task<string> GetUploadUrlAsync(Receive receive);
}
