using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Enums;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

public class AccessPreCheckQuery : IAccessPreCheckQuery
{
    private readonly ICipherRepository _cipherRepository;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly ISingleActiveLeaseEvaluator _singleActiveLeaseEvaluator;
    private readonly ICurrentContext _currentContext;
    private readonly TimeProvider _timeProvider;

    public AccessPreCheckQuery(
        ICipherRepository cipherRepository,
        IGoverningRuleResolver resolver,
        IAccessLeaseRepository accessLeaseRepository,
        ISingleActiveLeaseEvaluator singleActiveLeaseEvaluator,
        ICurrentContext currentContext,
        TimeProvider timeProvider)
    {
        _cipherRepository = cipherRepository;
        _resolver = resolver;
        _accessLeaseRepository = accessLeaseRepository;
        _singleActiveLeaseEvaluator = singleActiveLeaseEvaluator;
        _currentContext = currentContext;
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
            // CanStartLease keeps its default true here rather than being computed: the client reveals the credential
            // instead of rendering a request form, so the field has nothing to qualify. Skips the extra query too.
            return new AccessPreCheckResult(AccessApprovalMode.Automatic, HasActiveLease: true);
        }

        var signals = AccessSignals.From(_currentContext.IpAddress, new DateTimeOffset(now, TimeSpan.Zero));
        var governingRule = await _resolver.ResolveAsync(userId, cipherId, signals);
        var approvalMode = governingRule?.RequiresHumanApproval == true
            ? AccessApprovalMode.Human
            : AccessApprovalMode.Automatic;

        // Publish the same bounds SubmitAccessRequestCommand enforces, so the client's duration picker offers only
        // durations that will be accepted. An ungated cipher resolves to no rule and falls back to the global bounds;
        // there is nothing to request against it anyway, so the values are inert rather than wrong.
        var maxDurationSeconds = LeaseDurationBounds.EffectiveMax(governingRule?.MaxLeaseDurationSeconds);
        var defaultDurationSeconds =
            LeaseDurationBounds.EffectiveDefault(governingRule?.DefaultLeaseDurationSeconds, maxDurationSeconds);

        // Whether a lease could actually be started right now — the spec's RuleAllowsLease. A hint only: the mint
        // procedure's UPDLOCK/HOLDLOCK range lock is authoritative and re-checks this at start.
        //
        // The !applies short-circuit is the point, not an optimization. A member with an ungated or
        // non-single_active_lease path to the cipher is unconstrained and must read as startable however many leases
        // are live, per the same union/OR rule that governs gating — and the extra query stays off that path.
        var blockingLease = await _singleActiveLeaseEvaluator.AppliesAsync(userId, cipherId)
            ? await _accessLeaseRepository.GetActiveByCipherIdAsync(cipherId, now)
            : null;

        return new AccessPreCheckResult(
            approvalMode,
            HasActiveLease: false,
            DefaultDurationSeconds: defaultDurationSeconds,
            MaxDurationSeconds: maxDurationSeconds,
            CanStartLease: blockingLease is null,
            SlotFreesAt: blockingLease?.NotAfter);
    }
}
