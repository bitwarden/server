using Bit.Pam.Entities;
using Bit.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Pam.Repositories;
using Bit.Pam.Services;

namespace Bit.Pam.OrganizationFeatures.Queries;

public class ListActiveLeasesQuery : IListActiveLeasesQuery
{
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly TimeProvider _timeProvider;

    public ListActiveLeasesQuery(
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IAccessLeaseRepository accessLeaseRepository,
        TimeProvider timeProvider)
    {
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _accessLeaseRepository = accessLeaseRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ICollection<AccessLease>> GetActiveAsync(Guid userId)
    {
        var manageableCollectionIds = await _approverCollectionAccessQuery.GetManageableCollectionIdsAsync(userId);
        if (manageableCollectionIds.Count == 0)
        {
            return new List<AccessLease>();
        }

        return await _accessLeaseRepository.GetManyActiveByCollectionIdsAsync(
            manageableCollectionIds, _timeProvider.GetUtcNow().UtcDateTime);
    }
}
