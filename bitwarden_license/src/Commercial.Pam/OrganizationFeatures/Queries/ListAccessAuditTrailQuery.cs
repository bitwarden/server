using Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Pam.Models;
using Bit.Pam.Repositories;

namespace Bit.Commercial.Pam.OrganizationFeatures.Queries;

public class ListAccessAuditTrailQuery : IListAccessAuditTrailQuery
{
    private readonly IAccessAuditEventRepository _accessAuditEventRepository;
    private readonly TimeProvider _timeProvider;

    public ListAccessAuditTrailQuery(
        IAccessAuditEventRepository accessAuditEventRepository,
        TimeProvider timeProvider)
    {
        _accessAuditEventRepository = accessAuditEventRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ICollection<AccessAuditEvent>> GetTrailAsync(Guid organizationId)
    {
        // Shares the approver inbox's history window so the audit view reaches as far back as request/lease history.
        // @Now also dates the derived expiry events (an approved request whose window lapsed unused, a lease past its
        // window), so the projection can surface them without a sweep having run. Authorization is the AccessEventLogs
        // permission, enforced at the endpoint, so the trail is org-wide.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var since = now.AddDays(-ListInboxHistoryQuery.HistoryRetentionDays);
        return await _accessAuditEventRepository.GetManyByOrganizationIdAsync(organizationId, since, now);
    }
}
