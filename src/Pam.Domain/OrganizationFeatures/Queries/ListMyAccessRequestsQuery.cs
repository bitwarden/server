using Bit.Pam.Models;
using Bit.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Pam.Repositories;

namespace Bit.Pam.OrganizationFeatures.Queries;

public class ListMyAccessRequestsQuery : IListMyAccessRequestsQuery
{
    private readonly IAccessRequestRepository _accessRequestRepository;

    public ListMyAccessRequestsQuery(IAccessRequestRepository accessRequestRepository)
    {
        _accessRequestRepository = accessRequestRepository;
    }

    public Task<ICollection<AccessRequestDetails>> GetMineAsync(Guid userId) =>
        _accessRequestRepository.GetManyByRequesterIdAsync(userId);
}
