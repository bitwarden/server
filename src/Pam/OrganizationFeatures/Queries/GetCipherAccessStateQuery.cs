using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Engine;
using Bit.Pam.Entities;
using Bit.Pam.Models;
using Bit.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Pam.Repositories;
using Bit.Pam.Services;

namespace Bit.Pam.OrganizationFeatures.Queries;

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
        var activeLease = await _accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipherId, now);
        var pending = await _accessRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(userId, cipherId);
        var approved = await _accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(userId, cipherId, now);

        var extensionsAllowed = false;
        int? maxExtensionDurationSeconds = null;
        if (activeLease is not null)
        {
            // Extension eligibility drives the banner's "Extend" control. A lease may be extended once, so it is
            // extendable only while the rule opts in and no extension has been recorded yet; surface the rule's max
            // length so the client can cap its duration picker.
            var rule = await _resolver.ResolveAsync(userId, cipherId, signals);
            if (rule?.AllowsExtensions == true)
            {
                var used = await _accessRequestRepository.CountExtensionsByLeaseIdAsync(activeLease.Id);
                extensionsAllowed = used == 0;
                maxExtensionDurationSeconds = rule.MaxExtensionDurationSeconds;
            }
        }
        else if (pending is null && approved is null && await _resolver.ResolveAsync(userId, cipherId, signals) is null)
        {
            // Nothing to report and the cipher isn't leasing-gated. (When a lease or request exists we still return a
            // snapshot even if the rule was since removed, so the caller's state isn't hidden.)
            throw new NotFoundException();
        }

        return new CipherAccessState(
            cipherId,
            activeLease,
            pending is null ? null : ToDetails(pending),
            approved is null ? null : ToDetails(approved),
            extensionsAllowed,
            maxExtensionDurationSeconds);
    }

    // Neither a pending nor an approved-unactivated request has produced a lease (the approved read excludes
    // activated rows), and the approver identity/comment and inbox display-name fields aren't needed for this
    // caller-scoped snapshot, so they stay null.
    private static AccessRequestDetails ToDetails(AccessRequest request) => new()
    {
        Id = request.Id,
        ExtensionOfLeaseId = request.ExtensionOfLeaseId,
        OrganizationId = request.OrganizationId,
        CollectionId = request.CollectionId,
        CipherId = request.CipherId,
        RequesterId = request.RequesterId,
        NotBefore = request.NotBefore,
        NotAfter = request.NotAfter,
        Reason = request.Reason,
        Status = request.Status,
        CreationDate = request.CreationDate,
        ResolvedDate = request.ResolvedDate,
    };
}
