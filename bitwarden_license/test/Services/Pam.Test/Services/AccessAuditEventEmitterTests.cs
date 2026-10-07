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
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

[SutProviderCustomize]
public class AccessAuditEventEmitterTests
{
    /// <summary>A kind with no organization event log equivalent, so only the PAM store is exercised.</summary>
    private static AccessAuditEventData AnEvent(Guid organizationId) =>
        AnEventOfKind(organizationId, AccessAuditEventKind.RotationOffered);

    private static AccessAuditEventData AnEventOfKind(Guid organizationId, AccessAuditEventKind kind) => new()
    {
        Kind = kind,
        Phase = AccessAuditEventPhase.Outcome,
        OccurredDate = DateTime.UtcNow,
        OrganizationId = organizationId,
    };

    [Theory, BitAutoData]
    public async Task EmitAsync_PersistsEventToTheStore(
        Guid organizationId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        // The substitute reports every flag off.
        var auditEvent = AnEvent(organizationId);

        await sutProvider.Sut.EmitAsync(auditEvent);

        await sutProvider.GetDependency<IAccessAuditEventRepository>().Received(1).CreateAsync(auditEvent);
    }

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

    // A failed attempt stops the action.
    [Theory, BitAutoData]
    public async Task EmitAsync_AttemptStoreFailure_Throws(
        Guid organizationId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        var auditEvent = AnEvent(organizationId) with { Phase = AccessAuditEventPhase.Attempt };
        sutProvider.GetDependency<IAccessAuditEventRepository>().CreateAsync(auditEvent)
            .ThrowsAsync(new InvalidOperationException());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.EmitAsync(auditEvent));
    }

    // The action has already happened, so a failed outcome does not fail it.
    [Theory, BitAutoData]
    public async Task EmitAsync_OutcomeStoreFailure_DoesNotThrow(
        Guid organizationId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        var auditEvent = AnEvent(organizationId) with { Phase = AccessAuditEventPhase.Outcome };
        sutProvider.GetDependency<IAccessAuditEventRepository>().CreateAsync(auditEvent)
            .ThrowsAsync(new InvalidOperationException());

        await sutProvider.Sut.EmitAsync(auditEvent);
    }

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
    [BitAutoData(AccessAuditEventKind.RequestCancelled, EventType.Pam_AccessRequest_Cancelled)]
    [BitAutoData(AccessAuditEventKind.LeaseActivated, EventType.Pam_AccessLease_Activated)]
    [BitAutoData(AccessAuditEventKind.LeaseActivationRejected, EventType.Pam_AccessLease_ActivationRejected)]
    [BitAutoData(AccessAuditEventKind.LeaseExtended, EventType.Pam_AccessLease_Extended)]
    [BitAutoData(AccessAuditEventKind.LeaseRevoked, EventType.Pam_AccessLease_Revoked)]
    [BitAutoData(AccessAuditEventKind.LeaseExpired, EventType.Pam_AccessLease_Expired)]
    [BitAutoData(AccessAuditEventKind.RuleCreated, EventType.Pam_AccessRule_Created)]
    [BitAutoData(AccessAuditEventKind.RuleUpdated, EventType.Pam_AccessRule_Updated)]
    [BitAutoData(AccessAuditEventKind.RuleDeleted, EventType.Pam_AccessRule_Deleted)]
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
            OccurredDate = occurredAt,
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

    // dbo.Event has no rule column, and a rule spans many collections, so the row records only who changed it and when.
    [Theory, BitAutoData]
    public async Task EmitAsync_WithARuleKind_CarriesTheActorButNoSubject(
        Guid organizationId, Guid actorId, Guid accessRuleId,
        SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        var auditEvent = AnEventOfKind(organizationId, AccessAuditEventKind.RuleUpdated) with
        {
            ActorId = actorId,
            AccessRuleId = accessRuleId,
            RuleName = "Production database",
        };

        await sutProvider.Sut.EmitAsync(auditEvent);

        await sutProvider.GetDependency<IEventService>().Received(1).LogPamAccessEventAsync(
            EventType.Pam_AccessRule_Updated,
            Arg.Is<PamAccessEventContext>(c =>
                c.OrganizationId == organizationId &&
                c.ActingUserId == actorId &&
                c.CipherId == null &&
                c.CollectionId == null &&
                c.AccessRequestId == null &&
                c.AccessLeaseId == null));
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

    // Rotation and fleet subjects have no dbo.Event column, so a fan-out would file high-volume rows with nothing to
    // identify them.
    [Theory]
    [BitAutoData(AccessAuditEventKind.RotationOffered)]
    [BitAutoData(AccessAuditEventKind.RotationSucceeded)]
    [BitAutoData(AccessAuditEventKind.ManualRotationDue)]
    [BitAutoData(AccessAuditEventKind.AccessConnectorRegistered)]
    [BitAutoData(AccessAuditEventKind.TargetSystemRegistered)]
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

    [Theory, BitAutoData]
    public async Task EmitAsync_OutcomeStoreFailure_StillWritesToTheOrganizationEventLog(
        Guid organizationId, SutProvider<AccessAuditEventEmitter> sutProvider)
    {
        var auditEvent = AnEventOfKind(organizationId, AccessAuditEventKind.RequestApproved);
        sutProvider.GetDependency<IAccessAuditEventRepository>().CreateAsync(auditEvent)
            .ThrowsAsync(new InvalidOperationException());

        await sutProvider.Sut.EmitAsync(auditEvent);

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogPamAccessEventAsync(EventType.Pam_AccessRequest_Approved, Arg.Any<PamAccessEventContext>());
    }
}
