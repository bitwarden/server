using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
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
        // userId is neither the lease holder (lease.RequesterId is a different AutoFixture Guid) nor a manager.
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

        // An operator (manager, not the holder) ended it → settles to Revoked.
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

    [Theory, BitAutoData]
    public async Task RevokeAsync_WindowAlreadyClosed_ThrowsConflictWithoutEndingTheLease(
        Guid userId, AccessLease lease)
    {
        // A lease whose window has closed carries no early end -- expiry is never stored -- so ending it here would
        // restate a lease that ran out on its own as an operator revocation, stamping RevokedDate/RevokedBy and
        // appending a Deny decision for an end that already happened (PM-42355).
        var sutProvider = Setup();
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now.AddMinutes(-1);
        SetupManageableLease(sutProvider, userId, lease);

        await Assert.ThrowsAsync<ConflictException>(() => sutProvider.Sut.RevokeAsync(userId, lease.Id, null));

        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .RevokeAsync(default!, default, default!, default);
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_WindowClosesExactlyNow_ThrowsConflict(Guid userId, AccessLease lease)
    {
        // NotAfter is exclusive everywhere else (the active reads use NotAfter > now), so the boundary instant is
        // already outside the window.
        var sutProvider = Setup();
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now;
        SetupManageableLease(sutProvider, userId, lease);

        await Assert.ThrowsAsync<ConflictException>(() => sutProvider.Sut.RevokeAsync(userId, lease.Id, null));
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
