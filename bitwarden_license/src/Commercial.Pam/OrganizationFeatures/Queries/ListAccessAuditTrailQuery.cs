using Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Commercial.Pam.Services;
using Bit.Pam.Models;
using Bit.Pam.Repositories;

namespace Bit.Commercial.Pam.OrganizationFeatures.Queries;

public class ListAccessAuditTrailQuery : IListAccessAuditTrailQuery
{
    private readonly IApproverCollectionAccessQuery _approverCollectionAccessQuery;
    private readonly IAccessAuditEventRepository _accessAuditEventRepository;
    private readonly TimeProvider _timeProvider;

    public ListAccessAuditTrailQuery(
        IApproverCollectionAccessQuery approverCollectionAccessQuery,
        IAccessAuditEventRepository accessAuditEventRepository,
        TimeProvider timeProvider)
    {
        _approverCollectionAccessQuery = approverCollectionAccessQuery;
        _accessAuditEventRepository = accessAuditEventRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ICollection<AccessAuditEvent>> GetTrailAsync(Guid userId)
    {
        var manageableCollectionIds = await _approverCollectionAccessQuery.GetManageableCollectionIdsAsync(userId);
        if (manageableCollectionIds.Count == 0)
        {
            return new List<AccessAuditEvent>();
        }

        // Shares the approver inbox's history window so the audit view reaches as far back as request/lease history.
        // @Now also dates the derived expiry events (an approved request whose window lapsed unused, a lease past its
        // window), so the projection can surface them without a sweep having run.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var since = now.AddDays(-ListInboxHistoryQuery.HistoryRetentionDays);
        return await _accessAuditEventRepository.GetManyByCollectionIdsAsync(manageableCollectionIds, since, now);
    }
}
