using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;

namespace Bit.Core.Pam.OrganizationFeatures.Queries;

public class GetInboxHistoryQuery : IGetInboxHistoryQuery
{
    /// <summary>
    /// How far back the resolved history reaches. Older activity may be omitted. v1 has no pagination.
    /// </summary>
    public const int HistoryRetentionDays = 90;

    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly ILeaseRequestRepository _leaseRequestRepository;
    private readonly TimeProvider _timeProvider;

    public GetInboxHistoryQuery(
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        ILeaseRequestRepository leaseRequestRepository,
        TimeProvider timeProvider)
    {
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _leaseRequestRepository = leaseRequestRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ICollection<InboxLeaseRequestDetails>> GetHistoryAsync(Guid userId)
    {
        var manageableCollectionIds = await _approverCollectionAccessQuery.GetManageableCollectionIdsAsync(userId);
        if (manageableCollectionIds.Count == 0)
        {
            return new List<InboxLeaseRequestDetails>();
        }

        var since = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-HistoryRetentionDays);
        return await _leaseRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(manageableCollectionIds, since);
    }
}
