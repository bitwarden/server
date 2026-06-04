using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Pam.Repositories;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Pam.OrganizationFeatures.Queries;

public class GetLeasedCipherQuery : IGetLeasedCipherQuery
{
    private readonly ICipherRepository _cipherRepository;
    private readonly ILeaseRepository _leaseRepository;
    private readonly TimeProvider _timeProvider;

    public GetLeasedCipherQuery(
        ICipherRepository cipherRepository,
        ILeaseRepository leaseRepository,
        TimeProvider timeProvider)
    {
        _cipherRepository = cipherRepository;
        _leaseRepository = leaseRepository;
        _timeProvider = timeProvider;
    }

    public async Task<CipherDetails?> GetLeasedCipherAsync(Guid userId, Guid cipherId)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Without an active lease whose window contains now, the caller is not entitled to the full data right now.
        var lease = await _leaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipherId, now);
        if (lease is null)
        {
            return null;
        }

        // GetByIdAsync filters by access, so a null result means the caller cannot see the cipher.
        return await _cipherRepository.GetByIdAsync(cipherId, userId);
    }
}
