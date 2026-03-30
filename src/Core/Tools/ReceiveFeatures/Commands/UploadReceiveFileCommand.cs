using Bit.Core.Tools.Entities;
using Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;

namespace Bit.Core.Tools.ReceiveFeatures.Commands;

public class UploadReceiveFileCommand(IReceiveFileStorageService receiveFileStorageService) : IUploadReceiveFileCommand
{
    public async Task<string> GetUploadUrlAsync(Receive receive)
    {
        var fileId = CoreHelpers.SecureRandomString(32, upper: false, special: false);
        return await receiveFileStorageService.GetReceiveFileUploadUrlAsync(receive, fileId);
    }
}
