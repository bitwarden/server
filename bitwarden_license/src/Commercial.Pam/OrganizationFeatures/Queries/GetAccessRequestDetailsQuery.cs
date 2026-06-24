using Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Commercial.Pam.Services;
using Bit.Core.Exceptions;
using Bit.Pam.Models;
using Bit.Pam.Repositories;

namespace Bit.Commercial.Pam.OrganizationFeatures.Queries;

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

    public async Task<AccessRequestDetails> GetDetailsAsync(Guid userId, Guid requestId)
    {
        var details = await _accessRequestRepository.GetDetailsByIdAsync(requestId);

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
