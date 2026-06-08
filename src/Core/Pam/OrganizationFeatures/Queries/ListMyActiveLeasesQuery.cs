using Bit.Core.Pam.Entities;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Pam.Repositories;

namespace Bit.Core.Pam.OrganizationFeatures.Queries;

public class ListMyActiveLeasesQuery : IListMyActiveLeasesQuery
{
    private readonly ILeaseRepository _leaseRepository;
    private readonly TimeProvider _timeProvider;

    public ListMyActiveLeasesQuery(ILeaseRepository leaseRepository, TimeProvider timeProvider)
    {
        _leaseRepository = leaseRepository;
        _timeProvider = timeProvider;
    }

    public Task<ICollection<Lease>> GetMineActiveAsync(Guid userId) =>
        _leaseRepository.GetManyActiveByRequesterIdAsync(userId, _timeProvider.GetUtcNow().UtcDateTime);
}
