using Bit.Services.Pam.Models;
namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IGetCipherAccessStateQuery
{
    /// <summary>
    /// Returns the caller's lease state for a single leasing-gated cipher — their active lease and pending
    /// request, if any. Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> if the caller cannot see
    /// the cipher, or the cipher is not leasing-gated and there is nothing to report.
    /// </summary>
    Task<CipherAccessState> GetStateAsync(Guid userId, Guid cipherId);
}
