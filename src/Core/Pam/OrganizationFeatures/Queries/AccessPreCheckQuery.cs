using Bit.Core.Exceptions;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Pam.OrganizationFeatures.Queries;

public class AccessPreCheckQuery : IAccessPreCheckQuery
{
    private readonly ICipherRepository _cipherRepository;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly TimeProvider _timeProvider;

    public AccessPreCheckQuery(
        ICipherRepository cipherRepository,
        IGoverningRuleResolver resolver,
        IAccessLeaseRepository accessLeaseRepository,
        TimeProvider timeProvider)
    {
        _cipherRepository = cipherRepository;
        _resolver = resolver;
        _accessLeaseRepository = accessLeaseRepository;
        _timeProvider = timeProvider;
    }

    public async Task<AccessPreCheckResult> PreCheckAsync(Guid userId, Guid cipherId)
    {
        // GetByIdAsync filters by access, so a null result means the caller cannot see the cipher.
        var cipher = await _cipherRepository.GetByIdAsync(cipherId, userId);
        if (cipher is null)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // A caller who already holds an active lease should be sent straight to the credential, not prompted to make
        // a request that SubmitAccessRequestCommand would reject. This mirrors the active-lease guard there.
        if (await _accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipherId, now) is not null)
        {
            return new AccessPreCheckResult(AccessApprovalMode.Automatic, HasActiveLease: true);
        }

        var governingRule = await _resolver.ResolveAsync(userId, cipherId);
        var approvalMode = governingRule?.RequiresHumanApproval == true
            ? AccessApprovalMode.Human
            : AccessApprovalMode.Automatic;

        return new AccessPreCheckResult(approvalMode);
    }
}
