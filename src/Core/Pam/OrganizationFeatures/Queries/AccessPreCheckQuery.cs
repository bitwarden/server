using Bit.Core.Exceptions;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Pam.Services;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Pam.OrganizationFeatures.Queries;

public class AccessPreCheckQuery : IAccessPreCheckQuery
{
    private readonly ICipherRepository _cipherRepository;
    private readonly IAccessApprovalResolver _resolver;

    public AccessPreCheckQuery(ICipherRepository cipherRepository, IAccessApprovalResolver resolver)
    {
        _cipherRepository = cipherRepository;
        _resolver = resolver;
    }

    public async Task<AccessPreCheckResult> PreCheckAsync(Guid userId, Guid cipherId)
    {
        // GetByIdAsync filters by access, so a null result means the caller cannot see the cipher.
        var cipher = await _cipherRepository.GetByIdAsync(cipherId, userId);
        if (cipher is null)
        {
            throw new NotFoundException();
        }

        var resolution = await _resolver.ResolveAsync(userId, cipherId);
        var outcome = resolution?.RequiresHumanApproval == true
            ? AccessApprovalOutcome.Human
            : AccessApprovalOutcome.Automatic;

        return new AccessPreCheckResult(outcome);
    }
}
