using Bit.Core.Exceptions;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Pam.OrganizationFeatures.Queries;

public class GetCipherLeaseStateQuery : IGetCipherLeaseStateQuery
{
    private readonly ICipherRepository _cipherRepository;
    private readonly IAccessApprovalResolver _resolver;
    private readonly ILeaseRepository _leaseRepository;
    private readonly ILeaseRequestRepository _leaseRequestRepository;
    private readonly TimeProvider _timeProvider;

    public GetCipherLeaseStateQuery(
        ICipherRepository cipherRepository,
        IAccessApprovalResolver resolver,
        ILeaseRepository leaseRepository,
        ILeaseRequestRepository leaseRequestRepository,
        TimeProvider timeProvider)
    {
        _cipherRepository = cipherRepository;
        _resolver = resolver;
        _leaseRepository = leaseRepository;
        _leaseRequestRepository = leaseRequestRepository;
        _timeProvider = timeProvider;
    }

    public async Task<CipherLeaseStateResult> GetStateAsync(Guid userId, Guid cipherId)
    {
        // GetByIdAsync filters by access, so a null result means the caller cannot see the cipher.
        var cipher = await _cipherRepository.GetByIdAsync(cipherId, userId);
        if (cipher is null)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var activeLease = await _leaseRepository.GetActiveByRequesterIdCipherIdAsync(userId, cipherId, now);
        var pending = await _leaseRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(userId, cipherId);

        // 404 when the cipher isn't leasing-gated and there's nothing to report. We still return a snapshot when the
        // caller holds a lease or a pending request even if the rule was since removed, so their state isn't hidden.
        if (activeLease is null && pending is null && await _resolver.ResolveAsync(userId, cipherId) is null)
        {
            throw new NotFoundException();
        }

        return new CipherLeaseStateResult(cipherId, activeLease, pending is null ? null : ToDetails(pending));
    }

    // A pending request has produced no lease and has no resolver yet; the inbox display-name fields aren't needed for
    // this caller-scoped snapshot, so they stay null.
    private static InboxLeaseRequestDetails ToDetails(LeaseRequest request) => new()
    {
        Id = request.Id,
        ExtensionOfLeaseId = request.LeaseId,
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
