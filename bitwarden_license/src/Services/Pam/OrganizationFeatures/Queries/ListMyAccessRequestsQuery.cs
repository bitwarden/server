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
        // No collection-manageability check here, unlike the approver reads: being the requester is the whole
        // authorization story. `now` bounds the history window, gates unlapsed approved requests, and projects
        // each row's derived statuses.
        return await _accessRequestRepository.GetManyByRequesterIdAsync(
            userId, now.AddDays(-AccessHistoryWindow.RetentionDays), now);
    }
}
