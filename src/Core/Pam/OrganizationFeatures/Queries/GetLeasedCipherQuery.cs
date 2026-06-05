using System.Net;
using Bit.Core.Context;
using Bit.Core.Pam.Engine;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Pam.OrganizationFeatures.Queries;

public class GetLeasedCipherQuery : IGetLeasedCipherQuery
{
    private readonly ICipherRepository _cipherRepository;
    private readonly ILeaseRepository _leaseRepository;
    private readonly IAccessApprovalResolver _resolver;
    private readonly IAccessPolicyEngine _policyEngine;
    private readonly ICurrentContext _currentContext;
    private readonly TimeProvider _timeProvider;

    public GetLeasedCipherQuery(
        ICipherRepository cipherRepository,
        ILeaseRepository leaseRepository,
        IAccessApprovalResolver resolver,
        IAccessPolicyEngine policyEngine,
        ICurrentContext currentContext,
        TimeProvider timeProvider)
    {
        _cipherRepository = cipherRepository;
        _leaseRepository = leaseRepository;
        _resolver = resolver;
        _policyEngine = policyEngine;
        _currentContext = currentContext;
        _timeProvider = timeProvider;
    }

    public async Task<CipherDetails?> GetLeasedCipherAsync(Guid userId, Guid cipherId)
    {
        var now = _timeProvider.GetUtcNow();

        // Without an active lease whose window contains now, the caller is not entitled to the full data right now.
        var lease = await _leaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipherId, now.UtcDateTime);
        if (lease is null)
        {
            return null;
        }

        // A lease grants a window, but the access rule's environmental conditions (source IP, time of day) must
        // still hold at the moment the data is handed over. Approval is not re-checked here: holding the lease is
        // proof it was already granted, so only an outright denial withholds the data.
        var resolution = await _resolver.ResolveAsync(userId, cipherId);
        if (resolution is not null)
        {
            var signals = new AccessPolicySignals
            {
                IpAddress = IPAddress.TryParse(_currentContext.IpAddress, out var ip) ? ip : null,
                Timestamp = now,
            };

            if (_policyEngine.Evaluate(resolution.Rule, signals).Kind == DecisionKind.Deny)
            {
                return null;
            }
        }

        // GetByIdAsync filters by access, so a null result means the caller cannot see the cipher.
        return await _cipherRepository.GetByIdAsync(cipherId, userId);
    }
}
