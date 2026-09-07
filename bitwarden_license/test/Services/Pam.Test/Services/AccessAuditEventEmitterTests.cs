using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

[SutProviderCustomize]
public class AccessAuditEventEmitterTests
{
    /// <summary>An event of a kind with no organization event log equivalent, so only the PAM store is exercised.</summary>
    private static AccessAuditEventData AnEvent(Guid organizationId) =>
        AnEventOfKind(organizationId, AccessAuditEventKind.RuleCreated);

    private static AccessAuditEventData AnEventOfKind(Guid organizationId, AccessAuditEventKind kind) => new()
    {
        Kind = kind,
        Phase = AccessAuditEventPhase.Outcome,
        OccurredAt = DateTime.UtcNow,
        OrganizationId = organizationId,
    };

    [Theory, BitAutoData]
    public async Task EmitAsync_PersistsEventToTheStore(
        Guid organizationId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        // The substituted feature service reports every flag off, which is the kill switch's absent-flag default.
        var auditEvent = AnEvent(organizationId);

        await sutProvider.Sut.EmitAsync(auditEvent);

        await sutProvider.GetDependency<IAccessAuditEventRepository>().Received(1).CreateAsync(auditEvent);
    }

    // The kill switch stops the write itself, not merely hides the trail, shedding inserts under store pressure.
    [Theory, BitAutoData]
    public async Task EmitAsync_WithSqlAuditLoggingDisabled_WritesNothing(
        Guid organizationId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        sutProvider.GetDependency<Bitwarden.Server.Sdk.Features.IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging)
            .Returns(true);

        await sutProvider.Sut.EmitAsync(AnEvent(organizationId));

        await sutProvider.GetDependency<IAccessAuditEventRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default!);
    }

    // The kill switch is scoped to its own store; the organization event log is a separate sink.
    [Theory, BitAutoData]
    public async Task EmitAsync_WithSqlAuditLoggingDisabled_StillWritesToTheOrganizationEventLog(
        Guid organizationId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        sutProvider.GetDependency<Bitwarden.Server.Sdk.Features.IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging)
            .Returns(true);

        await sutProvider.Sut.EmitAsync(AnEventOfKind(organizationId, AccessAuditEventKind.RequestSubmitted));

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogPamAccessEventAsync(EventType.Pam_AccessRequest_Submitted, Arg.Any<PamAccessEventContext>());
    }

    [Theory]
    [BitAutoData(AccessAuditEventKind.RequestSubmitted, EventType.Pam_AccessRequest_Submitted)]
    [BitAutoData(AccessAuditEventKind.RequestApproved, EventType.Pam_AccessRequest_Approved)]
    [BitAutoData(AccessAuditEventKind.RequestDenied, EventType.Pam_AccessRequest_Denied)]
    [BitAutoData(AccessAuditEventKind.LeaseActivated, EventType.Pam_AccessLease_Activated)]
    [BitAutoData(AccessAuditEventKind.LeaseRevoked, EventType.Pam_AccessLease_Revoked)]
    public async Task EmitAsync_WithAMappedKind_WritesToTheOrganizationEventLog(
        AccessAuditEventKind kind, EventType expectedType, Guid organizationId,
        SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        await sutProvider.Sut.EmitAsync(AnEventOfKind(organizationId, kind));

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogPamAccessEventAsync(expectedType, Arg.Any<PamAccessEventContext>());
    }

    [Theory, BitAutoData]
    public async Task EmitAsync_WithAMappedKind_CarriesTheEventsFactsAcross(
        Guid organizationId, Guid actorId, Guid requesterId, Guid cipherId, Guid collectionId,
        Guid accessRequestId, Guid accessLeaseId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        var occurredAt = DateTime.UtcNow.AddMinutes(-5);
        var auditEvent = AnEventOfKind(organizationId, AccessAuditEventKind.LeaseRevoked) with
        {
            OccurredAt = occurredAt,
            ActorId = actorId,
            RequesterId = requesterId,
            CipherId = cipherId,
            CollectionId = collectionId,
            AccessRequestId = accessRequestId,
            AccessLeaseId = accessLeaseId,
        };

        await sutProvider.Sut.EmitAsync(auditEvent);

        await sutProvider.GetDependency<IEventService>().Received(1).LogPamAccessEventAsync(
            EventType.Pam_AccessLease_Revoked,
            Arg.Is<PamAccessEventContext>(c =>
                c.OrganizationId == organizationId &&
                c.Date == occurredAt &&
                c.ActingUserId == actorId &&
                c.UserId == requesterId &&
                c.CipherId == cipherId &&
                c.CollectionId == collectionId &&
                c.AccessRequestId == accessRequestId &&
                c.AccessLeaseId == accessLeaseId &&
                c.SystemUser == null));
    }

    // No actor means PAM acted on its own; the org log needs it named as the system user.
    [Theory, BitAutoData]
    public async Task EmitAsync_WithNoActor_AttributesTheEventToPam(
        Guid organizationId, Guid requesterId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        var auditEvent = AnEventOfKind(organizationId, AccessAuditEventKind.RequestDenied) with
        {
            ActorId = null,
            RequesterId = requesterId,
        };

        await sutProvider.Sut.EmitAsync(auditEvent);

        await sutProvider.GetDependency<IEventService>().Received(1).LogPamAccessEventAsync(
            EventType.Pam_AccessRequest_Denied,
            Arg.Is<PamAccessEventContext>(c => c.SystemUser == EventSystemUser.Pam && c.ActingUserId == null));
    }

    // dbo.Event has no phase/correlation column, so emitting the Attempt too would double every action.
    [Theory, BitAutoData]
    public async Task EmitAsync_WithAnAttempt_WritesOnlyToTheStore(
        Guid organizationId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        var auditEvent = AnEventOfKind(organizationId, AccessAuditEventKind.LeaseActivated) with
        {
            Phase = AccessAuditEventPhase.Attempt,
        };

        await sutProvider.Sut.EmitAsync(auditEvent);

        await sutProvider.GetDependency<IAccessAuditEventRepository>().Received(1).CreateAsync(auditEvent);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogPamAccessEventAsync(default, default!);
    }

    // Most kinds are PAM-internal detail (rotation and daemon lifecycle) and are deliberately not reported org-wide.
    [Theory]
    [BitAutoData(AccessAuditEventKind.RuleCreated)]
    [BitAutoData(AccessAuditEventKind.RequestCancelled)]
    [BitAutoData(AccessAuditEventKind.LeaseExtended)]
    [BitAutoData(AccessAuditEventKind.RotationOffered)]
    [BitAutoData(AccessAuditEventKind.DaemonRegistered)]
    public async Task EmitAsync_WithAnUnmappedKind_WritesOnlyToTheStore(
        AccessAuditEventKind kind, Guid organizationId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        var auditEvent = AnEventOfKind(organizationId, kind);

        await sutProvider.Sut.EmitAsync(auditEvent);

        await sutProvider.GetDependency<IAccessAuditEventRepository>().Received(1).CreateAsync(auditEvent);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogPamAccessEventAsync(default, default!);
    }

    // The PAM store is already written by fan-out time; a fan-out throw shouldn't fail the access decision.
    [Theory, BitAutoData]
    public async Task EmitAsync_WhenTheOrganizationEventLogFails_DoesNotDisturbTheCaller(
        Guid organizationId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        var auditEvent = AnEventOfKind(organizationId, AccessAuditEventKind.RequestApproved);
        sutProvider.GetDependency<IEventService>()
            .LogPamAccessEventAsync(Arg.Any<EventType>(), Arg.Any<PamAccessEventContext>())
            .ThrowsAsync(new InvalidOperationException("the event queue is unreachable"));

        await sutProvider.Sut.EmitAsync(auditEvent);

        await sutProvider.GetDependency<IAccessAuditEventRepository>().Received(1).CreateAsync(auditEvent);
    }
}
