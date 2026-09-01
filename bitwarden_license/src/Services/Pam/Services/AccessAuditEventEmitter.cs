using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Services.Pam.Services;

/// <inheritdoc cref="IAccessAuditEventEmitter" />
public class AccessAuditEventEmitter : IAccessAuditEventEmitter
{
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService;
    private readonly IAccessAuditEventRepository _accessAuditEventRepository;
    private readonly IEventService _eventService;
    private readonly ILogger<AccessAuditEventEmitter> _logger;

    public AccessAuditEventEmitter(
        Bitwarden.Server.Sdk.Features.IFeatureService featureService,
        IAccessAuditEventRepository accessAuditEventRepository,
        IEventService eventService,
        ILogger<AccessAuditEventEmitter> logger)
    {
        _featureService = featureService;
        _accessAuditEventRepository = accessAuditEventRepository;
        _eventService = eventService;
        _logger = logger;
    }

    public async Task EmitAsync(AccessAuditEventData auditEvent)
    {
        // The kill switch is read per call rather than at registration so flipping it takes effect on the next
        // request instead of the next deployment — the whole point of having it. Dropping the event here, at the
        // seam, is what keeps the commands unaware: they still await the emit and their outcome is unchanged.
        //
        // It gates the PAM store only, which is what its name and its purpose say: shedding those inserts when the
        // audit store is under pressure. The organization event log is a separate sink with its own capacity, so the
        // fan-out below stays outside the guard.
        if (!_featureService.IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging))
        {
            // Persist to the dedicated PAM audit store. Deliberately not enlisted in the caller's transaction (there
            // is none): under the before/after model the Attempt is written ahead of the action and the Outcome after
            // it, so a failure in between leaves an in-doubt Attempt rather than a silently lost event.
            await _accessAuditEventRepository.CreateAsync(auditEvent);
        }

        await FanOutToOrganizationEventLogAsync(auditEvent);
    }

    /// <summary>
    /// Copies the event into the organization's event log, where PAM activity sits alongside the rest of the
    /// organization's audit history (and reaches the event integrations that read from it). The PAM store remains the
    /// system of record: this is a lossy projection, since <c>dbo.Event</c> can represent neither the event's phase nor
    /// the PAM-specific subjects (the rule, target system, daemon, rotation config and job) it has no column for.
    /// </summary>
    private async Task FanOutToOrganizationEventLogAsync(AccessAuditEventData auditEvent)
    {
        // Only the Outcome half crosses over. There is no phase or correlation column on the other side, so writing
        // the Attempt as well would double every action in the organization event log while adding nothing a reader
        // could act on; the in-doubt Attempt an interrupted action leaves behind stays visible in the PAM trail.
        if (auditEvent.Phase != AccessAuditEventPhase.Outcome)
        {
            return;
        }

        var eventType = MapToOrganizationEventType(auditEvent.Kind);
        if (eventType is null)
        {
            return;
        }

        var context = new PamAccessEventContext
        {
            OrganizationId = auditEvent.OrganizationId,
            // The time PAM recorded the action, not the time this fan-out ran, so both trails agree.
            Date = auditEvent.OccurredAt,
            ActingUserId = auditEvent.ActorId,
            UserId = auditEvent.RequesterId,
            // dbo.Event already has columns for these two, so the item and its gated collection cross over as
            // first-class fields rather than as text. That is what files a PAM event under the item's own event
            // history alongside the ordinary vault events for it.
            CipherId = auditEvent.CipherId,
            CollectionId = auditEvent.CollectionId,
            AccessRequestId = auditEvent.AccessRequestId,
            AccessLeaseId = auditEvent.AccessLeaseId,
            // No actor means PAM itself acted (an automatic decision, or a sweep), which the PAM trail renders as
            // Automated. Naming PAM as the system user is how the organization event log says the same thing, and
            // keeps its member column from simply being blank.
            SystemUser = auditEvent.ActorId is null ? EventSystemUser.Pam : null,
        };

        try
        {
            await _eventService.LogPamAccessEventAsync(eventType.Value, context);
        }
        catch (Exception ex)
        {
            // The PAM store is the system of record and has already been written by this point. Letting this throw
            // would undo nothing and would turn a hiccup in the event pipeline into a failed access decision, so the
            // fan-out is best-effort: the event is lost from the organization log, not from the audit trail.
            _logger.LogError(ex,
                "Failed to write PAM audit event {Kind} to the organization event log. The event is recorded in the PAM audit trail.",
                auditEvent.Kind);
        }
    }

    /// <summary>
    /// The PAM audit kinds that have an organization event log equivalent. A kind with no mapping is recorded in the
    /// PAM store only — deliberately, since most kinds are either PAM-internal detail (rotation and daemon lifecycle)
    /// or carry a subject the event schema has no column for. Extend this as each kind earns a place in the org-wide
    /// log, together with the matching <see cref="EventType"/> value and its client-side message.
    /// </summary>
    private static EventType? MapToOrganizationEventType(AccessAuditEventKind kind) => kind switch
    {
        AccessAuditEventKind.RequestSubmitted => EventType.Pam_AccessRequest_Submitted,
        AccessAuditEventKind.RequestApproved => EventType.Pam_AccessRequest_Approved,
        AccessAuditEventKind.RequestDenied => EventType.Pam_AccessRequest_Denied,
        AccessAuditEventKind.LeaseActivated => EventType.Pam_AccessLease_Activated,
        AccessAuditEventKind.LeaseRevoked => EventType.Pam_AccessLease_Revoked,
        _ => null,
    };
}
