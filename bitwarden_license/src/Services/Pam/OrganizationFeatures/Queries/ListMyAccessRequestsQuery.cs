using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

public class ListMyAccessRequestsQuery : IListMyAccessRequestsQuery
{
    private readonly IAccessRequestRepository _accessRequestRepository;

    public ListMyAccessRequestsQuery(
        IAccessRequestRepository accessRequestRepository)
    {
        _accessRequestRepository = accessRequestRepository;
    }

    public async Task<ICollection<AccessRequestDetails>> GetMineAsync(Guid userId, DateTime now)
    {
        // Being the requester is the whole authorization; no collection check.
        return await _accessRequestRepository.GetManyByRequesterIdAsync(
            userId, now.AddDays(-AccessHistoryWindow.RetentionDays), now);
    }
}
