using Bit.Core.Exceptions;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

public class GetAccessRequestDetailsQuery : IGetAccessRequestDetailsQuery
{
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly TimeProvider _timeProvider;

    public GetAccessRequestDetailsQuery(
        IAccessRequestRepository accessRequestRepository,
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        TimeProvider timeProvider)
    {
        _accessRequestRepository = accessRequestRepository;
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _timeProvider = timeProvider;
    }

    public async Task<AccessRequestDetails> GetDetailsAsync(Guid userId, Guid requestId)
    {
        // The clock the produced lease's status is projected against; see AccessRequestDetails.ProducedLeaseStatus.
        var details = await _accessRequestRepository.GetDetailsByIdAsync(
            requestId, _timeProvider.GetUtcNow().UtcDateTime);

        // 404 when the request is missing or the caller is neither its requester nor a managing approver, so the caller
        // can't probe for requests they have no business seeing. Mirrors the cancel/decide surfaces. Being a read, this
        // does NOT block the requester from viewing their own request (decide does, to forbid self-approval).
        if (details is null)
        {
            throw new NotFoundException();
        }

        var isRequester = details.RequesterId == userId;
        var isManager = !isRequester
            && await _approverCollectionAccessQuery.CanManageCollectionAsync(userId, details.CollectionId);
        if (!isRequester && !isManager)
        {
            throw new NotFoundException();
        }

        return details;
    }
}
