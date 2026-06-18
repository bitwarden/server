using Bit.Pam.Models;
using Bit.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Pam.Repositories;
using Bit.Pam.Services;

namespace Bit.Pam.OrganizationFeatures.Queries;

public class ListInboxRequestsQuery : IListInboxRequestsQuery
{
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IAccessRequestRepository _accessRequestRepository;

    public ListInboxRequestsQuery(
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IAccessRequestRepository accessRequestRepository)
    {
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _accessRequestRepository = accessRequestRepository;
    }

    public async Task<ICollection<AccessRequestDetails>> GetPendingAsync(Guid userId)
    {
        var manageableCollectionIds = await _approverCollectionAccessQuery.GetManageableCollectionIdsAsync(userId);
        if (manageableCollectionIds.Count == 0)
        {
            return new List<AccessRequestDetails>();
        }

        return await _accessRequestRepository.GetManyInboxPendingByCollectionIdsAsync(manageableCollectionIds);
    }
}
