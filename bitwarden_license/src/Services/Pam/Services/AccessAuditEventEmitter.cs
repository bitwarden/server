using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;

namespace Bit.Services.Pam.Services;

/// <inheritdoc cref="IAccessAuditEventEmitter" />
public class AccessAuditEventEmitter : IAccessAuditEventEmitter
{
    /// <summary>The width of <c>[AccessAuditEvent].[IpAddress]</c>.</summary>
    private const int IpAddressMaxLength = 50;

    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService;
    private readonly IAccessAuditEventRepository _accessAuditEventRepository;
    private readonly IEventService _eventService;
    private readonly ICurrentContext _currentContext;
    private readonly ILogger<AccessAuditEventEmitter> _logger;

    public AccessAuditEventEmitter(
        Bitwarden.Server.Sdk.Features.IFeatureService featureService,
        IAccessAuditEventRepository accessAuditEventRepository,
        IEventService eventService,
        ICurrentContext currentContext,
        ILogger<AccessAuditEventEmitter> logger)
    {
        _featureService = featureService;
        _accessAuditEventRepository = accessAuditEventRepository;
        _eventService = eventService;
        _currentContext = currentContext;
        _logger = logger;
    }

    public async Task EmitAsync(AccessAuditEventData auditEvent)
    {
        var enrichedEvent = WithRequestSource(auditEvent);

        // Gates the PAM store only, not the organization event log fan-out.
        if (!_featureService.IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging))
        {
            try
            {
                await _accessAuditEventRepository.CreateAsync(enrichedEvent);
            }
            catch (Exception ex) when (enrichedEvent.Phase == AccessAuditEventPhase.Outcome)
            {
                _logger.LogError(ex,
                    "Failed to record the outcome of PAM audit event {Kind} ({CorrelationId}). The trail shows the action as incomplete.",
                    enrichedEvent.Kind, enrichedEvent.CorrelationId);
            }
        }

        await FanOutToOrganizationEventLogAsync(enrichedEvent);
    }

    /// <summary>
    /// Stamps the IP address and device type of the current request. A background job has no request, so both stay
    /// null. An automatic decision made inside a request, such as an auto-approval, records the IP it was evaluated
    /// on. The IP is cut to the column width, since an oversized value would fail the write and block the action.
    /// </summary>
    private AccessAuditEventData WithRequestSource(AccessAuditEventData auditEvent)
    {
        var ipAddress = _currentContext.IpAddress;
        return auditEvent with
        {
            IpAddress = ipAddress is { Length: > IpAddressMaxLength } ? ipAddress[..IpAddressMaxLength] : ipAddress,
            DeviceType = _currentContext.DeviceType,
        };
    }

    /// <summary>
    /// Copies the event into the organization's event log. The PAM store remains the system of record, since
    /// <c>dbo.Event</c> has no column for the event's phase, rule or detail.
    /// </summary>
    private async Task FanOutToOrganizationEventLogAsync(AccessAuditEventData auditEvent)
    {
        // Only the Outcome half crosses over; there is no phase or correlation column on the other side.
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
            Date = auditEvent.OccurredDate,
            ActingUserId = auditEvent.ActorId,
            UserId = auditEvent.RequesterId,
            // dbo.Event has columns for these two, so a PAM event files under the item's own vault event history.
            CipherId = auditEvent.CipherId,
            CollectionId = auditEvent.CollectionId,
            AccessRequestId = auditEvent.AccessRequestId,
            AccessLeaseId = auditEvent.AccessLeaseId,
            // No actor means PAM itself acted; naming it as the system user keeps the member column non-blank.
            SystemUser = auditEvent.ActorId is null ? EventSystemUser.Pam : null,
        };

        try
        {
            await _eventService.LogPamAccessEventAsync(eventType.Value, context);
        }
        catch (Exception ex)
        {
            // Best-effort: the action has already happened, so a failure here must not fail it.
            _logger.LogError(ex,
                "Failed to write PAM audit event {Kind} to the organization event log. The event is recorded in the PAM audit trail.",
                auditEvent.Kind);
        }
    }

    /// <summary>
    /// The PAM audit kinds that have an organization event log equivalent. A kind with no mapping is recorded in
    /// the PAM store only.
    /// </summary>
    private static EventType? MapToOrganizationEventType(AccessAuditEventKind kind) => kind switch
    {
        AccessAuditEventKind.RequestSubmitted => EventType.Pam_AccessRequest_Submitted,
        AccessAuditEventKind.RequestApproved => EventType.Pam_AccessRequest_Approved,
        AccessAuditEventKind.RequestDenied => EventType.Pam_AccessRequest_Denied,
        AccessAuditEventKind.RequestCancelled => EventType.Pam_AccessRequest_Cancelled,
        AccessAuditEventKind.LeaseActivated => EventType.Pam_AccessLease_Activated,
        AccessAuditEventKind.LeaseActivationRejected => EventType.Pam_AccessLease_ActivationRejected,
        AccessAuditEventKind.LeaseExtended => EventType.Pam_AccessLease_Extended,
        AccessAuditEventKind.LeaseRevoked => EventType.Pam_AccessLease_Revoked,
        AccessAuditEventKind.LeaseExpired => EventType.Pam_AccessLease_Expired,
        AccessAuditEventKind.RuleCreated => EventType.Pam_AccessRule_Created,
        AccessAuditEventKind.RuleUpdated => EventType.Pam_AccessRule_Updated,
        AccessAuditEventKind.RuleDeleted => EventType.Pam_AccessRule_Deleted,
        AccessAuditEventKind.RotationSucceeded => EventType.Pam_Rotation_Succeeded,
        // A timed-out job counts as failed; the PAM store keeps the reason.
        AccessAuditEventKind.RotationFailed or AccessAuditEventKind.RotationJobTimedOut =>
            EventType.Pam_Rotation_Failed,
        _ => null,
    };
}
