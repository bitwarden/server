using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

public class ListLeaseHistoryQuery : IListLeaseHistoryQuery
{
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IAccessLeaseRepository _accessLeaseRepository;

    public ListLeaseHistoryQuery(
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IAccessLeaseRepository accessLeaseRepository)
    {
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _accessLeaseRepository = accessLeaseRepository;
    }

    public async Task<ICollection<AccessLease>> GetHistoryAsync(Guid userId, DateTime now)
    {
        var manageableCollectionIds = await _approverCollectionAccessQuery.GetManageableCollectionIdsAsync(userId);
        if (manageableCollectionIds.Count == 0)
        {
            return new List<AccessLease>();
        }

        // Shares the one history window (AccessHistoryWindow) so request history and lease history reach equally
        // far back. `now` is the caller's read clock: it additionally decides which leases count as ended at all --
        // a lapsed lease is only Expired relative to a clock (see
        // IAccessLeaseRepository.GetManyEndedByCollectionIdsAsync) -- and the caller derives response statuses
        // against the same instant.
        return await _accessLeaseRepository.GetManyEndedByCollectionIdsAsync(
            manageableCollectionIds, now.AddDays(-AccessHistoryWindow.RetentionDays), now);
    }
}
