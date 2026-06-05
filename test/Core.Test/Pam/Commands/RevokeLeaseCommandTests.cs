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
public class RevokeLeaseCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task RevokeAsync_LeaseMissing_ThrowsNotFound(Guid userId, Guid leaseId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<ILeaseRepository>().GetByIdAsync(leaseId).Returns((Lease?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.RevokeAsync(userId, leaseId, null));
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_NotManageable_ThrowsNotFound(Guid userId, Lease lease)
    {
        var sutProvider = Setup();
        lease.Status = LeaseStatus.Active;
        sutProvider.GetDependency<ILeaseRepository>().GetByIdAsync(lease.Id).Returns(lease);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(userId, lease.CollectionId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.RevokeAsync(userId, lease.Id, null));
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_NotActive_ThrowsConflict(Guid userId, Lease lease)
    {
        var sutProvider = Setup();
        lease.Status = LeaseStatus.Revoked;
        SetupManageableLease(sutProvider, userId, lease);

        await Assert.ThrowsAsync<ConflictException>(() => sutProvider.Sut.RevokeAsync(userId, lease.Id, null));
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_Active_RevokesAndWritesAuditDecision(Guid userId, Lease lease)
    {
        var sutProvider = Setup();
        lease.Status = LeaseStatus.Active;
        SetupManageableLease(sutProvider, userId, lease);

        await sutProvider.Sut.RevokeAsync(userId, lease.Id, "policy change");

        await sutProvider.GetDependency<ILeaseRepository>().Received(1).RevokeAsync(
            lease,
            Arg.Is<LeaseDecision>(d =>
                d.LeaseRequestId == lease.LeaseRequestId &&
                d.DeciderKind == LeaseDecisionKind.Human &&
                d.ApproverId == userId &&
                d.Decision == LeaseDecisionVerdict.Deny &&
                d.Comment == "policy change"),
            _now);
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(lease.CollectionId);
    }

    private static SutProvider<RevokeLeaseCommand> Setup()
    {
        var sutProvider = new SutProvider<RevokeLeaseCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static void SetupManageableLease(SutProvider<RevokeLeaseCommand> sutProvider, Guid userId, Lease lease)
    {
        sutProvider.GetDependency<ILeaseRepository>().GetByIdAsync(lease.Id).Returns(lease);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(userId, lease.CollectionId).Returns(true);
    }
}
