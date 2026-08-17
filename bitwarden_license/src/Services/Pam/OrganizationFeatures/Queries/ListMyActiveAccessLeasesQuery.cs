using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

public class ListMyActiveAccessLeasesQuery : IListMyActiveAccessLeasesQuery
{
    private readonly IAccessLeaseRepository _accessLeaseRepository;
    private readonly TimeProvider _timeProvider;

    public ListMyActiveAccessLeasesQuery(IAccessLeaseRepository accessLeaseRepository, TimeProvider timeProvider)
    {
        _accessLeaseRepository = accessLeaseRepository;
        _timeProvider = timeProvider;
    }

    public Task<ICollection<AccessLease>> GetMineActiveAsync(Guid userId) =>
        _accessLeaseRepository.GetManyActiveByRequesterIdAsync(userId, _timeProvider.GetUtcNow().UtcDateTime);
}
