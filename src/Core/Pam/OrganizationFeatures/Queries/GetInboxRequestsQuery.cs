using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;

namespace Bit.Core.Pam.OrganizationFeatures.Queries;

public class GetInboxRequestsQuery : IGetInboxRequestsQuery
{
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly ILeaseRequestRepository _leaseRequestRepository;

    public GetInboxRequestsQuery(
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        ILeaseRequestRepository leaseRequestRepository)
    {
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _leaseRequestRepository = leaseRequestRepository;
    }

    public async Task<ICollection<InboxLeaseRequestDetails>> GetPendingAsync(Guid userId)
    {
        var manageableCollectionIds = await _approverCollectionAccessQuery.GetManageableCollectionIdsAsync(userId);
        if (manageableCollectionIds.Count == 0)
        {
            return new List<InboxLeaseRequestDetails>();
        }

        return await _leaseRequestRepository.GetManyInboxPendingByCollectionIdsAsync(manageableCollectionIds);
    }
}
