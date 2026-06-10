using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Pam.Repositories;

namespace Bit.Core.Pam.OrganizationFeatures.Queries;

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
