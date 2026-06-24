using Bit.Commercial.Pam.OrganizationFeatures.Commands;
using Bit.Commercial.Pam.Services;
using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Pam.Test.Commands;

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
        lease.Status = AccessLeaseStatus.Active;
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
        lease.Status = AccessLeaseStatus.Active;
        // The caller IS the lease's own holder, but cannot Manage the collection — they may still end their own access.
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByIdAsync(lease.Id).Returns(lease);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(lease.RequesterId, lease.CollectionId).Returns(false);

        await sutProvider.Sut.RevokeAsync(lease.RequesterId, lease.Id, "done with it");

        // Settles to Cancelled (the holder ended their own access) with the holder recorded as the revoker.
        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1).RevokeAsync(
            lease,
            AccessLeaseStatus.Cancelled,
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
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_NotActive_ThrowsConflict(Guid userId, AccessLease lease)
    {
        var sutProvider = Setup();
        lease.Status = AccessLeaseStatus.Revoked;
        SetupManageableLease(sutProvider, userId, lease);

        await Assert.ThrowsAsync<ConflictException>(() => sutProvider.Sut.RevokeAsync(userId, lease.Id, null));
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_Active_RevokesAndWritesAuditDecision(Guid userId, AccessLease lease)
    {
        var sutProvider = Setup();
        lease.Status = AccessLeaseStatus.Active;
        SetupManageableLease(sutProvider, userId, lease);

        await sutProvider.Sut.RevokeAsync(userId, lease.Id, "policy change");

        // An operator (manager, not the holder) ended it → settles to Revoked.
        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1).RevokeAsync(
            lease,
            AccessLeaseStatus.Revoked,
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
