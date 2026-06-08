using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Pam.Repositories;

namespace Bit.Core.Pam.OrganizationFeatures.Queries;

public class ListMyAccessRequestsQuery : IListMyAccessRequestsQuery
{
    private readonly ILeaseRequestRepository _leaseRequestRepository;

    public ListMyAccessRequestsQuery(ILeaseRequestRepository leaseRequestRepository)
    {
        _leaseRequestRepository = leaseRequestRepository;
    }

    public Task<ICollection<InboxLeaseRequestDetails>> GetMineAsync(Guid userId) =>
        _leaseRequestRepository.GetManyByRequesterIdAsync(userId);
}
