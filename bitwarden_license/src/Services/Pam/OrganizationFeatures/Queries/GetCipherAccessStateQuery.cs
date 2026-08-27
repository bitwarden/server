using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

public class GetCipherAccessStateQuery : IGetCipherAccessStateQuery
{
    private readonly ICipherRepository _cipherRepository;
    private readonly IGoverningRuleResolver _resolver;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly ICurrentContext _currentContext;
    private readonly TimeProvider _timeProvider;

    public GetCipherAccessStateQuery(
        ICipherRepository cipherRepository,
        IGoverningRuleResolver resolver,
        IAccessLeaseRepository accessLeaseRepository,
        IAccessRequestRepository accessRequestRepository,
        ICurrentContext currentContext,
        TimeProvider timeProvider)
    {
        _cipherRepository = cipherRepository;
        _resolver = resolver;
        _accessLeaseRepository = accessLeaseRepository;
        _accessRequestRepository = accessRequestRepository;
        _currentContext = currentContext;
        _timeProvider = timeProvider;
    }

    public async Task<CipherAccessState> GetStateAsync(Guid userId, Guid cipherId)
    {
        // GetByIdAsync filters by access, so a null result means the caller cannot see the cipher.
        var cipher = await _cipherRepository.GetByIdAsync(cipherId, userId);
        if (cipher is null)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var signals = AccessSignals.From(_currentContext.IpAddress, new DateTimeOffset(now, TimeSpan.Zero));

        // Four independent reads (each repository/resolver call opens its own connection/scope), fetched
        // concurrently: this snapshot runs per gated cipher on the vault path. The resolver's result goes unused in
        // the rare pending/approved states, but starting it eagerly saves its round trips on the two common paths
        // (active lease: extension eligibility; nothing at all: the gated-or-not verdict) and awaiting it inside the
        // WhenAll keeps any resolver failure observed.
        var activeLeaseTask = _accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipherId, now);
        var pendingTask = _accessRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(userId, cipherId, now);
        var approvedTask = _accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(userId, cipherId, now);
        var ruleTask = _resolver.ResolveAsync(userId, cipherId, signals);
        await Task.WhenAll(activeLeaseTask, pendingTask, approvedTask, ruleTask);
        var activeLease = await activeLeaseTask;
        var pending = await pendingTask;
        var approved = await approvedTask;

        var extensionsAllowed = false;
        int? maxExtensionDurationSeconds = null;
        if (activeLease is not null)
        {
            // Extension eligibility drives the banner's "Extend" control. A lease may be extended once, so it is
            // extendable only while the rule opts in and no extension has been recorded yet; surface the rule's max
            // length so the client can cap its duration picker.
            var rule = await ruleTask;
            if (rule?.AllowsExtensions == true)
            {
                var used = await _accessRequestRepository.CountExtensionsByLeaseIdAsync(activeLease.Id);
                extensionsAllowed = used == 0;
                maxExtensionDurationSeconds = rule.MaxExtensionDurationSeconds;
            }
        }
        else if (pending is null && approved is null && await ruleTask is null)
        {
            // Nothing to report and the cipher isn't leasing-gated. (When a lease or request exists we still return a
            // snapshot even if the rule was since removed, so the caller's state isn't hidden.)
            throw new NotFoundException();
        }

        // Neither a pending nor an approved-unactivated request has produced a lease (the approved read excludes
        // activated rows), and the approver identity/comment and inbox display-name fields aren't needed for this
        // caller-scoped snapshot, so they stay null. The status derives against the same clock that filtered the
        // reads, so it lands on Pending/Approved by construction.
        return new CipherAccessState(
            cipherId,
            now,
            activeLease,
            pending is null ? null : AccessRequestDetails.From(pending, now),
            approved is null ? null : AccessRequestDetails.From(approved, now),
            extensionsAllowed,
            maxExtensionDurationSeconds);
    }
}
