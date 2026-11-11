using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

public class ListInboxHistoryQuery : IListInboxHistoryQuery
{
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IAccessRequestRepository _accessRequestRepository;

    public ListInboxHistoryQuery(
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IAccessRequestRepository accessRequestRepository)
    {
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _accessRequestRepository = accessRequestRepository;
    }

    public async Task<AccessRequestHistoryPage> GetHistoryAsync(Guid userId, DateTime now,
        AccessRequestHistoryQueryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // A member who manages no collection still reads their own requests.
        var manageableCollectionIds = await _approverCollectionAccessQuery.GetManageableCollectionIdsAsync(userId);

        return await _accessRequestRepository.GetHistoryPageAsync(new AccessRequestHistoryFilter
        {
            ManageableCollectionIds = manageableCollectionIds,
            UserId = userId,
            Since = now.AddDays(-AccessHistoryWindow.RetentionDays),
            Now = now,
            Skip = options.Page * options.PageSize,
            Take = options.PageSize,
            Statuses = options.Statuses,
            CollectionIds = options.CollectionIds,
            RequesterIds = options.RequesterIds,
        });
    }
}
