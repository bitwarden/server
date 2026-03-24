using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

public interface IReceiveValidationService
{
    /// <summary>
    /// Validates a file can be saved by an anonymous user, for retrieval by a Receive owner.
    /// </summary>
    /// <param name="receive"><see cref="Receive" /> needed to help validate file</param>
    /// <returns>Task completes when a conditional statement has been met it will return out of the method or
    /// throw a BadRequestException.
    /// </returns>
    void ValidateUpload(Receive receive);

    /// <summary>
    /// Calculates the remaining storage for a Receive.
    /// </summary>
    /// <param name="receive"><see cref="Receive" /> needed to help calculate remaining storage</param>
    /// <returns>Long with the remaining bytes for storage.
    /// </returns>
    Task<long> StorageRemainingForReceiveAsync(Receive receive);
}
