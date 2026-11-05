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

        // Rule resolution waits for these: a held lease resolves the rule it was granted under.
        var activeLeaseTask = _accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipherId, now);
        var pendingTask = _accessRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(userId, cipherId, now);
        var approvedTask = _accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(userId, cipherId, now);
        await Task.WhenAll(activeLeaseTask, pendingTask, approvedTask);
        var activeLease = await activeLeaseTask;
        var pending = await pendingTask;
        var approved = await approvedTask;

        var extensionsAllowed = false;
        int? maxExtensionDurationSeconds = null;
        if (activeLease is not null)
        {
            // Extendable once under the lease's own rule, as RequestLeaseExtensionCommand enforces.
            var originatingRequest = await _accessRequestRepository.GetByIdAsync(activeLease.AccessRequestId);
            var rule = originatingRequest?.RuleId is { } ruleId
                ? await _resolver.ResolvePinnedAsync(ruleId, activeLease.CollectionId)
                : await _resolver.ResolveAsync(userId, cipherId, signals);
            if (rule?.AllowsExtensions == true)
            {
                var used = await _accessRequestRepository.CountExtensionsByLeaseIdAsync(activeLease.Id);
                extensionsAllowed = used == 0;
                maxExtensionDurationSeconds = rule.MaxExtensionDurationSeconds;
            }
        }
        else if (pending is null && approved is null
                 && await _resolver.ResolveAsync(userId, cipherId, signals) is null)
        {
            // Nothing to report and the cipher isn't leasing-gated.
            throw new NotFoundException();
        }

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
