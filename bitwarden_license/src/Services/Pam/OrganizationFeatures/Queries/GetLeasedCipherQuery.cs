using Bit.Services.Pam.Engine;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Services.Pam.Services;
using Bit.Core.Context;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Repositories;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

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

        var signals = AccessSignals.From(_currentContext.IpAddress, now);

        // A lease grants a window, but the access rule's environmental conditions (source IP, time of day) must
        // still hold at the moment the data is handed over. Approval is not re-checked here: holding the lease is
        // proof it was already granted, so only an outright denial withholds the data.
        var governingRule = await _resolver.ResolveAsync(userId, cipherId, signals);
        if (governingRule is not null
            && _ruleEngine.Evaluate(governingRule.Conditions, signals).Outcome == AccessEvaluationOutcome.Deny)
        {
            return null;
        }

        // GetByIdAsync filters by access, so a null result means the caller cannot see the cipher.
        return await _cipherRepository.GetByIdAsync(cipherId, userId);
    }
}
