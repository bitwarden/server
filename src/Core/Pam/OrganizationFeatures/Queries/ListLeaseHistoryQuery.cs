using Bit.Core.Pam.Entities;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;

namespace Bit.Core.Pam.OrganizationFeatures.Queries;

public class ListLeaseHistoryQuery : IListLeaseHistoryQuery
{
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly TimeProvider _timeProvider;

    public ListLeaseHistoryQuery(
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IAccessLeaseRepository accessLeaseRepository,
        TimeProvider timeProvider)
    {
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _accessLeaseRepository = accessLeaseRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ICollection<AccessLease>> GetHistoryAsync(Guid userId)
    {
        var manageableCollectionIds = await _approverCollectionAccessQuery.GetManageableCollectionIdsAsync(userId);
        if (manageableCollectionIds.Count == 0)
        {
            return new List<AccessLease>();
        }

        // Shares the approver inbox's history window so request history and lease history reach equally far back.
        var since = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-ListInboxHistoryQuery.HistoryRetentionDays);
        return await _accessLeaseRepository.GetManyEndedByCollectionIdsAsync(manageableCollectionIds, since);
    }
}
