using Bit.Core.Pam.Models;

namespace Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IGetCipherLeaseStateQuery
{
    /// <summary>
    /// Returns the caller's lease state for a single leasing-gated cipher — their active lease and pending request, if
    /// any. Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> when the caller cannot see the cipher, or when
    /// the cipher is not leasing-gated and the caller holds nothing to report.
    /// </summary>
    Task<CipherLeaseStateResult> GetStateAsync(Guid userId, Guid cipherId);
}
