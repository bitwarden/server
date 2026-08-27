using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

public class ListInboxHistoryQuery : IListInboxHistoryQuery
{
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IAccessRequestRepository _accessRequestRepository;

    public ListInboxHistoryQuery(
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IAccessRequestRepository accessRequestRepository)
    {
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _accessRequestRepository = accessRequestRepository;
    }

    public async Task<ICollection<AccessRequestDetails>> GetHistoryAsync(Guid userId, DateTime now)
    {
        var manageableCollectionIds = await _approverCollectionAccessQuery.GetManageableCollectionIdsAsync(userId);
        if (manageableCollectionIds.Count == 0)
        {
            return new List<AccessRequestDetails>();
        }

        // One clock (the caller's), two jobs: `now` bounds the history window through `since`, and separately
        // projects each row's derived statuses (see AccessRequestDetails.ProducedLeaseStatus).
        return await _accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            manageableCollectionIds, now.AddDays(-AccessHistoryWindow.RetentionDays), now);
    }
}
