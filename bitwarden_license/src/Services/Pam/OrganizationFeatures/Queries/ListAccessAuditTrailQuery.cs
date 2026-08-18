using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

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
        // Authorization is the AccessEventLogs permission, enforced at the endpoint, so the trail is org-wide.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var since = now.AddDays(-ListInboxHistoryQuery.HistoryRetentionDays);
        var events = await _accessAuditEventRepository.GetManyByOrganizationIdAsync(organizationId, since);

        // Collapse each action's before/after pair (shared CorrelationId) into one row: the Outcome when it landed,
        // otherwise the lone Attempt -- which the response flags as in-doubt (its outcome never arrived). Newest first.
        return events
            .GroupBy(auditEvent => auditEvent.CorrelationId)
            .Select(group =>
                group.FirstOrDefault(auditEvent => auditEvent.Phase == AccessAuditEventPhase.Outcome)
                ?? group.First())
            .OrderByDescending(auditEvent => auditEvent.OccurredAt)
            .ToList();
    }
}
