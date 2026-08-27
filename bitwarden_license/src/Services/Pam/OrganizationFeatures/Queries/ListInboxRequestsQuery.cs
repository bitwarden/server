using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

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

    public async Task<ICollection<AccessRequestDetails>> GetPendingAsync(Guid userId, DateTime now)
    {
        var manageableCollectionIds = await _approverCollectionAccessQuery.GetManageableCollectionIdsAsync(userId);
        if (manageableCollectionIds.Count == 0)
        {
            return new List<AccessRequestDetails>();
        }

        // `now` is the caller's read clock: it decides which rows are still actionable at all (a lapsed unanswered
        // request is derived Expired, leaves this inbox, and surfaces in the history read instead), and the statuses
        // stamped on the returned details derive against the same instant.
        return await _accessRequestRepository.GetManyInboxPendingByCollectionIdsAsync(manageableCollectionIds, now);
    }
}
