using Bit.Core.Pam.Entities;

namespace Bit.Core.Pam.Repositories;

public interface ILeaseRequestRepository
{
    Task<LeaseRequest> CreateAsync(LeaseRequest request);

    Task<LeaseRequest?> GetByIdAsync(Guid id);

    /// <summary>
    /// Returns the caller's pending (unresolved) lease request for the cipher, or null if there is none.
    /// </summary>
    Task<LeaseRequest?> GetActivePendingByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId);
}
