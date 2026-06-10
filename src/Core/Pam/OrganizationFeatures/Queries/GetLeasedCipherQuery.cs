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
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessRuleEngine _ruleEngine;
    private readonly ICurrentContext _currentContext;
    private readonly TimeProvider _timeProvider;

    public GetLeasedCipherQuery(
        ICipherRepository cipherRepository,
        IAccessLeaseRepository accessLeaseRepository,
        IGoverningRuleResolver resolver,
        IAccessRuleEngine ruleEngine,
        ICurrentContext currentContext,
        TimeProvider timeProvider)
    {
        _cipherRepository = cipherRepository;
        _accessLeaseRepository = accessLeaseRepository;
        _resolver = resolver;
        _ruleEngine = ruleEngine;
        _currentContext = currentContext;
        _timeProvider = timeProvider;
    }

    public async Task<CipherDetails?> GetLeasedCipherAsync(Guid userId, Guid cipherId)
    {
        var now = _timeProvider.GetUtcNow();

        // Without an active lease whose window contains now, the caller is not entitled to the full data right now.
        var lease = await _accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipherId, now.UtcDateTime);
        if (lease is null)
        {
            return null;
        }

        // A lease grants a window, but the access rule's environmental conditions (source IP, time of day) must
        // still hold at the moment the data is handed over. Approval is not re-checked here: holding the lease is
        // proof it was already granted, so only an outright denial withholds the data.
        var governingRule = await _resolver.ResolveAsync(userId, cipherId);
        if (governingRule is not null)
        {
            var signals = new AccessSignals
            {
                IpAddress = IPAddress.TryParse(_currentContext.IpAddress, out var ip) ? ip : null,
                Timestamp = now,
            };

            if (_ruleEngine.Evaluate(governingRule.Condition, signals).Outcome == AccessEvaluationOutcome.Deny)
            {
                return null;
            }
        }

        // GetByIdAsync filters by access, so a null result means the caller cannot see the cipher.
        return await _cipherRepository.GetByIdAsync(cipherId, userId);
    }
}
