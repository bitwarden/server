using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

public interface ISendValidationService
{
    /// <summary>
    /// Gets the maximum file size for a Send.
    /// </summary>
    /// <returns></returns>
    long GetMaxFileSize();

    /// <summary>
    /// Gets the maximum file size for a Send in a human-readable format.
    /// </summary>
    /// <returns></returns>
    string GetMaxFileSizeReadable();

    /// <summary>
    /// Validates the Send file.
    /// </summary>
    /// <param name="send"></param>
    /// <returns></returns>
    Task<bool> ValidateSendFile(Send send);

    /// <summary>
    /// Validates a file can be saved by specified user.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="send"></param>
    /// <returns></returns>
    Task ValidateUserCanSaveAsync(Guid? userId, Send send);

    /// <summary>
    /// Validates a file can be saved by specified user with different policy based on feature flag
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="send"></param>
    /// <returns></returns>
    Task ValidateUserCanSaveAsync_vNext(Guid? userId, Send send);

    /// <summary>
    /// Calculates the remaining storage for a Send.
    /// </summary>
    /// <param name="send"></param>
    /// <returns></returns>
    Task<long> StorageRemainingForSendAsync(Send send);
}
