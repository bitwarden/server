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

        // The same window as request history, so both reach equally far back.
        return await _accessLeaseRepository.GetManyEndedByCollectionIdsAsync(
            manageableCollectionIds, now.AddDays(-AccessHistoryWindow.RetentionDays), now);
    }
}
