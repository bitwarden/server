using Bit.Core.Exceptions;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.OrganizationFeatures.Commands;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Commands;

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
    public async Task RevokeAsync_NotManageable_ThrowsNotFound(Guid userId, AccessLease lease)
    {
        var sutProvider = Setup();
        lease.Status = AccessLeaseStatus.Active;
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByIdAsync(lease.Id).Returns(lease);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(userId, lease.CollectionId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.RevokeAsync(userId, lease.Id, null));
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

        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1).RevokeAsync(
            lease,
            Arg.Is<AccessDecision>(d =>
                d.AccessRequestId == lease.AccessRequestId &&
                d.DeciderKind == AccessDeciderKind.Human &&
                d.ApproverId == userId &&
                d.Verdict == AccessDecisionVerdict.Deny &&
                d.Comment == "policy change"),
            _now);
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(lease.CollectionId);
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
