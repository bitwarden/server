using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.SendFeatures.Commands.Interfaces;

public interface IAnonymousSendCommand
{
    Task<(string, bool, bool)> GetSendFileDownloadUrlAsync(Send send, string fileId, string password);
}
