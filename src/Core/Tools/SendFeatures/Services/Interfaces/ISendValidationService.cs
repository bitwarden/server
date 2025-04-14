using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

public interface ISendValidationService
{
    long GetMaxFileSize();
    string GetMaxFileSizeReadable();
    Task<bool> ValidateSendFile(Send send);
    Task ValidateUserCanSaveAsync(Guid? userId, Send send);
    Task ValidateUserCanSaveAsync_vNext(Guid? userId, Send send);
    Task<long> StorageRemainingForSendAsync(Send send);
}
