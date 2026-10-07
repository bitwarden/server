using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Services.Pam.Test.Commands;

[SutProviderCustomize]
public class RevokeAccessLeaseCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task RevokeAsync_LeaseMissing_ThrowsNotFound(Guid userId, Guid leaseId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByIdAsync(leaseId).Returns((AccessLease?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.RevokeAsync(userId, leaseId, null));
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_NeitherHolderNorManageable_ThrowsNotFound(Guid userId, AccessLease lease)
    {
        var sutProvider = Setup();
        lease.Action = AccessLeaseAction.None;
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByIdAsync(lease.Id).Returns(lease);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(userId, lease.CollectionId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.RevokeAsync(userId, lease.Id, null));
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_HolderEndsOwnLease_RevokesWithoutManageRights(AccessLease lease)
    {
        var sutProvider = Setup();
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now.AddHours(1);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByIdAsync(lease.Id).Returns(lease);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(lease.RequesterId, lease.CollectionId).Returns(false);

        await sutProvider.Sut.RevokeAsync(lease.RequesterId, lease.Id, "done with it");

        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1).RevokeAsync(
            lease,
            AccessLeaseAction.Cancelled,
            Arg.Is<AccessDecision>(d =>
                d.AccessRequestId == lease.AccessRequestId &&
                d.DeciderKind == AccessDeciderKind.Human &&
                d.ApproverId == lease.RequesterId &&
                d.Verdict == AccessDecisionVerdict.Deny &&
                d.Comment == "done with it"),
            _now);
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(lease.CollectionId);
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(lease.RequesterId);
        await sutProvider.GetDependency<ILeaseRevokedMailNotifier>().Received(1)
            .NotifyLeaseEndedAsync(lease, AccessLeaseAction.Cancelled);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_HolderWhoCanAlsoManage_EndsAsCancelled(AccessLease lease)
    {
        var sutProvider = Setup();
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now.AddHours(1);
        SetupManageableLease(sutProvider, lease.RequesterId, lease);

        await sutProvider.Sut.RevokeAsync(lease.RequesterId, lease.Id, null);

        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1).RevokeAsync(
            lease, AccessLeaseAction.Cancelled, Arg.Any<AccessDecision>(), _now);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("   ")]
    public async Task RevokeAsync_BlankReason_RecordsNoComment(string reason, Guid userId, AccessLease lease)
    {
        var sutProvider = Setup();
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now.AddHours(1);
        SetupManageableLease(sutProvider, userId, lease);

        await sutProvider.Sut.RevokeAsync(userId, lease.Id, reason);

        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1).RevokeAsync(
            lease, AccessLeaseAction.Revoked, Arg.Is<AccessDecision>(d => d.Comment == null), _now);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_NotActive_ThrowsConflict(Guid userId, AccessLease lease)
    {
        var sutProvider = Setup();
        lease.Action = AccessLeaseAction.Revoked;
        SetupManageableLease(sutProvider, userId, lease);

        await Assert.ThrowsAsync<ConflictException>(() => sutProvider.Sut.RevokeAsync(userId, lease.Id, null));
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_Active_RevokesAndWritesAuditDecision(Guid userId, AccessLease lease)
    {
        var sutProvider = Setup();
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now.AddHours(1);
        SetupManageableLease(sutProvider, userId, lease);

        await sutProvider.Sut.RevokeAsync(userId, lease.Id, "policy change");

        // A manager who is not the holder ends it as Revoked.
        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1).RevokeAsync(
            lease,
            AccessLeaseAction.Revoked,
            Arg.Is<AccessDecision>(d =>
                d.AccessRequestId == lease.AccessRequestId &&
                d.DeciderKind == AccessDeciderKind.Human &&
                d.ApproverId == userId &&
                d.Verdict == AccessDecisionVerdict.Deny &&
                d.Comment == "policy change"),
            _now);
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(lease.CollectionId);
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(lease.RequesterId);
        await sutProvider.GetDependency<ILeaseRevokedMailNotifier>().Received(1)
            .NotifyLeaseEndedAsync(lease, AccessLeaseAction.Revoked);
    }

    // Ending by the holder and revocation by an operator are both LeaseRevoked.
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task RevokeAsync_Active_EmitsRevokedAttemptThenOutcome(
        bool isHolder, Guid operatorId, AccessLease lease)
    {
        var sutProvider = Setup();
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now.AddHours(1);
        var userId = isHolder ? lease.RequesterId : operatorId;
        SetupManageableLease(sutProvider, userId, lease);
        var emitted = CaptureEmitted(sutProvider);

        await sutProvider.Sut.RevokeAsync(userId, lease.Id, "policy change");

        Assert.Collection(emitted,
            attempt => Assert.Equal(AccessAuditEventPhase.Attempt, attempt.Phase),
            outcome => Assert.Equal(AccessAuditEventPhase.Outcome, outcome.Phase));
        Assert.All(emitted, e =>
        {
            Assert.Equal(AccessAuditEventKind.LeaseRevoked, e.Kind);
            Assert.Equal(userId, e.ActorId);
            Assert.Equal(lease.Id, e.AccessLeaseId);
            Assert.Equal("policy change", e.Detail);
        });
        Assert.Equal(emitted[0].CorrelationId, emitted[1].CorrelationId);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_RevokeFails_EmitsAttemptWithoutOutcome(Guid userId, AccessLease lease)
    {
        var sutProvider = Setup();
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now.AddHours(1);
        SetupManageableLease(sutProvider, userId, lease);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .RevokeAsync(lease, Arg.Any<AccessLeaseAction>(), Arg.Any<AccessDecision>(), _now)
            .ThrowsAsync(new InvalidOperationException());
        var emitted = CaptureEmitted(sutProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.RevokeAsync(userId, lease.Id, null));

        var attempt = Assert.Single(emitted);
        Assert.Equal(AccessAuditEventPhase.Attempt, attempt.Phase);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_NotActive_EmitsNothing(Guid userId, AccessLease lease)
    {
        var sutProvider = Setup();
        lease.Action = AccessLeaseAction.Revoked;
        SetupManageableLease(sutProvider, userId, lease);

        await Assert.ThrowsAsync<ConflictException>(() => sutProvider.Sut.RevokeAsync(userId, lease.Id, null));

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    private static List<AccessAuditEventData> CaptureEmitted(SutProvider<RevokeAccessLeaseCommand> sutProvider)
    {
        var emitted = new List<AccessAuditEventData>();
        sutProvider.GetDependency<IAccessAuditEventEmitter>().EmitAsync(Arg.Do<AccessAuditEventData>(emitted.Add));
        return emitted;
    }

    // Ending by the holder and revocation by an operator are both LeaseRevoked.
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task RevokeAsync_Active_EmitsRevokedAttemptThenOutcome(
        bool isHolder, Guid operatorId, AccessLease lease)
    {
        var sutProvider = Setup();
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now.AddHours(1);
        var userId = isHolder ? lease.RequesterId : operatorId;
        SetupManageableLease(sutProvider, userId, lease);
        var emitted = CaptureEmitted(sutProvider);

        await sutProvider.Sut.RevokeAsync(userId, lease.Id, "policy change");

        Assert.Collection(emitted,
            attempt => Assert.Equal(AccessAuditEventPhase.Attempt, attempt.Phase),
            outcome => Assert.Equal(AccessAuditEventPhase.Outcome, outcome.Phase));
        Assert.All(emitted, e =>
        {
            Assert.Equal(AccessAuditEventKind.LeaseRevoked, e.Kind);
            Assert.Equal(userId, e.ActorId);
            Assert.Equal(lease.Id, e.AccessLeaseId);
            Assert.Equal("policy change", e.Detail);
        });
        Assert.Equal(emitted[0].CorrelationId, emitted[1].CorrelationId);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_RevokeFails_EmitsAttemptWithoutOutcome(Guid userId, AccessLease lease)
    {
        var sutProvider = Setup();
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now.AddHours(1);
        SetupManageableLease(sutProvider, userId, lease);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .RevokeAsync(lease, Arg.Any<AccessLeaseAction>(), Arg.Any<AccessDecision>(), _now)
            .ThrowsAsync(new InvalidOperationException());
        var emitted = CaptureEmitted(sutProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.RevokeAsync(userId, lease.Id, null));

        var attempt = Assert.Single(emitted);
        Assert.Equal(AccessAuditEventPhase.Attempt, attempt.Phase);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_NotActive_EmitsNothing(Guid userId, AccessLease lease)
    {
        var sutProvider = Setup();
        lease.Action = AccessLeaseAction.Revoked;
        SetupManageableLease(sutProvider, userId, lease);

        await Assert.ThrowsAsync<ConflictException>(() => sutProvider.Sut.RevokeAsync(userId, lease.Id, null));

        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs().EmitAsync(default!);
    }

    private static List<AccessAuditEventData> CaptureEmitted(SutProvider<RevokeAccessLeaseCommand> sutProvider)
    {
        var emitted = new List<AccessAuditEventData>();
        sutProvider.GetDependency<IAccessAuditEventEmitter>().EmitAsync(Arg.Do<AccessAuditEventData>(emitted.Add));
        return emitted;
    }

    private static SutProvider<RevokeAccessLeaseCommand> Setup()
    {
        var sutProvider = new SutProvider<RevokeAccessLeaseCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static void SetupManageableLease(SutProvider<RevokeAccessLeaseCommand> sutProvider, Guid userId, AccessLease lease)
    {
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByIdAsync(lease.Id).Returns(lease);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(userId, lease.CollectionId).Returns(true);
    }
}
