using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.SendFeatures.Commands.Interfaces;

/// <summary>
/// AnonymousSendCommand interface provides methods for managing anonymous Sends.
/// </summary>
public interface IAnonymousSendCommand
{
    Task<(string, bool, bool)> GetSendFileDownloadUrlAsync(Send send, string fileId, string password);
}
