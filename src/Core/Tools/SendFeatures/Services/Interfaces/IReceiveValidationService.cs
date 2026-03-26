using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

public interface IReceiveValidationService
{
    /// <summary>
    /// Calculates the remaining storage for a Receive.
    /// </summary>
    /// <param name="receive"><see cref="Receive" /> needed to help calculate remaining storage</param>
    /// <returns>Long with the remaining bytes for storage.
    /// </returns>
    Task<long> StorageRemainingForReceiveAsync(Receive receive);
}
