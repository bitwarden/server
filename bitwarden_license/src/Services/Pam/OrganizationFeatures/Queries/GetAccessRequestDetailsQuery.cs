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

    public GetAccessRequestDetailsQuery(
        IAccessRequestRepository accessRequestRepository,
        IApproverCollectionAccessQuery approverCollectionAccessQuery)
    {
        _accessRequestRepository = accessRequestRepository;
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
    }

    public async Task<AccessRequestDetails> GetDetailsAsync(Guid userId, Guid requestId, DateTime now)
    {
        // `now` is the caller's read clock, which the row's derived statuses are stamped against; see
        // AccessRequestDetails.ProducedLeaseStatus.
        var details = await _accessRequestRepository.GetDetailsByIdAsync(requestId, now);

        // 404 when the request is missing or the caller is neither its requester nor a managing approver. Unlike
        // decide, this does not block the requester from viewing their own request.
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
