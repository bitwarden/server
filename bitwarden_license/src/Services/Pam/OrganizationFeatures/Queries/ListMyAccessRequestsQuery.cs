using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

public class ListMyAccessRequestsQuery : IListMyAccessRequestsQuery
{
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly TimeProvider _timeProvider;

    public ListMyAccessRequestsQuery(
        IAccessRequestRepository accessRequestRepository,
        TimeProvider timeProvider)
    {
        _accessRequestRepository = accessRequestRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ICollection<AccessRequestDetails>> GetMineAsync(Guid userId)
    {
        // No collection-manageability check to make here, unlike the approver reads: this is a caller-scoped
        // self-read, and being the requester is the whole authorization story.
        //
        // One clock, three jobs: `now` bounds the history window through `since`, decides which approved requests
        // still have an unlapsed window (and so stay visible past that window), and projects each row's produced-lease
        // status (see AccessRequestDetails.ProducedLeaseStatus).
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return await _accessRequestRepository.GetManyByRequesterIdAsync(
            userId, now.AddDays(-AccessHistoryWindow.RetentionDays), now);
    }
}
