using Bit.Services.Pam.Models;
namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IGetCipherAccessStateQuery
{
    /// <summary>
    /// Returns the caller's active lease and pending and approved requests for a single leasing-gated cipher.
    /// Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> if the caller cannot see
    /// the cipher, or the cipher is not leasing-gated and there is nothing to report.
    /// </summary>
    Task<CipherAccessState> GetStateAsync(Guid userId, Guid cipherId);
}
