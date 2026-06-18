using Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Commercial.Pam.Services;
using Bit.Pam.Models;
using Bit.Pam.Repositories;

namespace Bit.Commercial.Pam.OrganizationFeatures.Queries;

public class ListInboxHistoryQuery : IListInboxHistoryQuery
{
    /// <summary>
    /// How far back the resolved history reaches. Older activity may be omitted. v1 has no pagination.
    /// </summary>
    public const int HistoryRetentionDays = 90;

    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly TimeProvider _timeProvider;

    public ListInboxHistoryQuery(
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IAccessRequestRepository accessRequestRepository,
        TimeProvider timeProvider)
    {
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _accessRequestRepository = accessRequestRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ICollection<AccessRequestDetails>> GetHistoryAsync(Guid userId)
    {
        var manageableCollectionIds = await _approverCollectionAccessQuery.GetManageableCollectionIdsAsync(userId);
        if (manageableCollectionIds.Count == 0)
        {
            return new List<AccessRequestDetails>();
        }

        var since = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-HistoryRetentionDays);
        return await _accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(manageableCollectionIds, since);
    }
}
