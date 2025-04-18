using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;

namespace Bit.Core.Tools.SendFeatures.Commands.Interfaces;

public interface INonAnonymousSendCommand
{
    Task SaveSendAsync(Send send);
    Task<string> SaveFileSendAsync(Send send, SendFileData data, long fileLength);
    Task UploadFileToExistingSendAsync(Stream stream, Send send);
    Task DeleteSendAsync(Send send);
}
