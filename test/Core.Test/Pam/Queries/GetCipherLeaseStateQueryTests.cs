using Bit.Core.Exceptions;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.Models.Rules;
using Bit.Core.Pam.OrganizationFeatures.Queries;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Queries;

[SutProviderCustomize]
public class GetCipherLeaseStateQueryTests
{
    [Theory, BitAutoData]
    public async Task GetStateAsync_CipherNotAccessible_ThrowsNotFound(
        SutProvider<GetCipherLeaseStateQuery> sutProvider, Guid userId, Guid cipherId)
    {
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns((CipherDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetStateAsync(userId, cipherId));
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_NotGatedAndNothingHeld_ThrowsNotFound(
        SutProvider<GetCipherLeaseStateQuery> sutProvider, Guid userId, Guid cipherId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        // No active lease, no pending request, and the resolver finds no governing rule.
        sutProvider.GetDependency<IAccessApprovalResolver>()
            .ResolveAsync(userId, cipherId)
            .Returns((AccessApprovalResolution?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetStateAsync(userId, cipherId));
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_ActiveLease_ReturnsSnapshotWithLease(
        SutProvider<GetCipherLeaseStateQuery> sutProvider, Guid userId, Guid cipherId, Lease activeLease)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<ILeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, Arg.Any<DateTime>())
            .Returns(activeLease);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.Equal(cipherId, result.CipherId);
        Assert.Same(activeLease, result.ActiveLease);
        Assert.Null(result.PendingRequest);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_LeaseHeldButRuleRemoved_StillReturnsSnapshot(
        SutProvider<GetCipherLeaseStateQuery> sutProvider, Guid userId, Guid cipherId, Lease activeLease)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<ILeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, Arg.Any<DateTime>())
            .Returns(activeLease);
        // Rule since removed: resolver returns null, but the held lease must not be hidden.
        sutProvider.GetDependency<IAccessApprovalResolver>()
            .ResolveAsync(userId, cipherId)
            .Returns((AccessApprovalResolution?)null);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.Same(activeLease, result.ActiveLease);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_PendingRequest_MapsToDetails(
        SutProvider<GetCipherLeaseStateQuery> sutProvider, Guid userId, Guid cipherId, LeaseRequest pending)
    {
        SetupCipher(sutProvider, userId, cipherId);
        pending.CipherId = cipherId;
        pending.RequesterId = userId;
        pending.Status = LeaseRequestStatus.Pending;
        sutProvider.GetDependency<ILeaseRequestRepository>()
            .GetActivePendingByRequesterIdCipherIdAsync(userId, cipherId)
            .Returns(pending);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.Null(result.ActiveLease);
        Assert.NotNull(result.PendingRequest);
        Assert.Equal(pending.Id, result.PendingRequest!.Id);
        Assert.Equal(pending.LeaseId, result.PendingRequest.ExtensionOfLeaseId);
        Assert.Equal(LeaseRequestStatus.Pending, result.PendingRequest.Status);
        // Pending has produced no lease and has no resolver yet; display-name fields are not populated.
        Assert.Null(result.PendingRequest.ProducedLeaseId);
        Assert.Null(result.PendingRequest.ResolverId);
        Assert.Null(result.PendingRequest.CipherName);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_GatedButEmpty_ReturnsEmptySnapshot(
        SutProvider<GetCipherLeaseStateQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessApprovalResolver>()
            .ResolveAsync(userId, cipherId)
            .Returns(new AccessApprovalResolution(orgId, collectionId, RequiresHumanApproval: true, new HumanApprovalRule()));

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.Equal(cipherId, result.CipherId);
        Assert.Null(result.ActiveLease);
        Assert.Null(result.PendingRequest);
    }

    private static void SetupCipher(SutProvider<GetCipherLeaseStateQuery> sutProvider, Guid userId, Guid cipherId)
    {
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns(new CipherDetails { Id = cipherId });
    }
}
