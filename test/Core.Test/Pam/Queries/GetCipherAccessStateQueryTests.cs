using Bit.Core.Exceptions;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Engine;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Models.Conditions;
using Bit.Pam.OrganizationFeatures.Queries;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Queries;

[SutProviderCustomize]
public class GetCipherAccessStateQueryTests
{
    [Theory, BitAutoData]
    public async Task GetStateAsync_CipherNotAccessible_ThrowsNotFound(
        SutProvider<GetCipherAccessStateQuery> sutProvider, Guid userId, Guid cipherId)
    {
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns((CipherDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetStateAsync(userId, cipherId));
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_NotGatedAndNothingHeld_ThrowsNotFound(
        SutProvider<GetCipherAccessStateQuery> sutProvider, Guid userId, Guid cipherId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        // No active lease, no pending request, and the resolver finds no governing rule.
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns((GoverningRule?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetStateAsync(userId, cipherId));
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_ActiveLease_ReturnsSnapshotWithLease(
        SutProvider<GetCipherAccessStateQuery> sutProvider, Guid userId, Guid cipherId, AccessLease activeLease)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, Arg.Any<DateTime>())
            .Returns(activeLease);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.Equal(cipherId, result.CipherId);
        Assert.Same(activeLease, result.ActiveLease);
        Assert.Null(result.PendingRequest);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_LeaseHeldButRuleRemoved_StillReturnsSnapshot(
        SutProvider<GetCipherAccessStateQuery> sutProvider, Guid userId, Guid cipherId, AccessLease activeLease)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, Arg.Any<DateTime>())
            .Returns(activeLease);
        // Access rule since removed: resolver returns null, but the held lease must not be hidden.
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns((GoverningRule?)null);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.Same(activeLease, result.ActiveLease);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_PendingRequest_MapsToDetails(
        SutProvider<GetCipherAccessStateQuery> sutProvider, Guid userId, Guid cipherId, AccessRequest pending)
    {
        SetupCipher(sutProvider, userId, cipherId);
        pending.CipherId = cipherId;
        pending.RequesterId = userId;
        pending.Status = AccessRequestStatus.Pending;
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetActivePendingByRequesterIdCipherIdAsync(userId, cipherId)
            .Returns(pending);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.Null(result.ActiveLease);
        Assert.NotNull(result.PendingRequest);
        Assert.Equal(pending.Id, result.PendingRequest!.Id);
        Assert.Equal(pending.ExtensionOfLeaseId, result.PendingRequest.ExtensionOfLeaseId);
        Assert.Equal(AccessRequestStatus.Pending, result.PendingRequest.Status);
        // Pending has produced no lease and has no resolver yet; display-name fields are not populated.
        Assert.Null(result.PendingRequest.ProducedLeaseId);
        Assert.Empty(result.PendingRequest.Decisions);
        Assert.Null(result.PendingRequest.CipherName);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_ApprovedRequest_MapsToDetails(
        SutProvider<GetCipherAccessStateQuery> sutProvider, Guid userId, Guid cipherId, AccessRequest approved)
    {
        SetupCipher(sutProvider, userId, cipherId);
        approved.CipherId = cipherId;
        approved.RequesterId = userId;
        approved.Status = AccessRequestStatus.Approved;
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetActiveApprovedByRequesterIdCipherIdAsync(userId, cipherId, Arg.Any<DateTime>())
            .Returns(approved);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.Null(result.ActiveLease);
        Assert.Null(result.PendingRequest);
        Assert.NotNull(result.ApprovedRequest);
        Assert.Equal(approved.Id, result.ApprovedRequest!.Id);
        Assert.Equal(AccessRequestStatus.Approved, result.ApprovedRequest.Status);
        Assert.Equal(approved.NotBefore, result.ApprovedRequest.NotBefore);
        Assert.Equal(approved.NotAfter, result.ApprovedRequest.NotAfter);
        // The approved read excludes activated rows, so no lease id; the caller-scoped snapshot carries no approver
        // identity or display-name fields.
        Assert.Null(result.ApprovedRequest.ProducedLeaseId);
        Assert.Empty(result.ApprovedRequest.Decisions);
        Assert.Null(result.ApprovedRequest.CipherName);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_ApprovedHeldButRuleRemoved_StillReturnsSnapshot(
        SutProvider<GetCipherAccessStateQuery> sutProvider, Guid userId, Guid cipherId, AccessRequest approved)
    {
        SetupCipher(sutProvider, userId, cipherId);
        approved.Status = AccessRequestStatus.Approved;
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetActiveApprovedByRequesterIdCipherIdAsync(userId, cipherId, Arg.Any<DateTime>())
            .Returns(approved);
        // Access rule since removed: resolver returns null, but the startable approval must not be hidden.
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns((GoverningRule?)null);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.NotNull(result.ApprovedRequest);
        Assert.Equal(approved.Id, result.ApprovedRequest!.Id);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_GatedButEmpty_ReturnsEmptySnapshot(
        SutProvider<GetCipherAccessStateQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(orgId, collectionId, RequiresHumanApproval: true,
                [new HumanApprovalCondition()]));

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.Equal(cipherId, result.CipherId);
        Assert.Null(result.ActiveLease);
        Assert.Null(result.PendingRequest);
        Assert.Null(result.ApprovedRequest);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_ActiveLease_NotYetExtended_AllowedWithMaxLength(
        SutProvider<GetCipherAccessStateQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId,
        AccessLease activeLease)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, Arg.Any<DateTime>())
            .Returns(activeLease);
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(orgId, collectionId, RequiresHumanApproval: false,
                [new HumanApprovalCondition()])
            {
                AllowsExtensions = true,
                MaxExtensionDurationSeconds = 4 * 60 * 60,
            });
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CountExtensionsByLeaseIdAsync(activeLease.Id).Returns(0);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.True(result.ExtensionsAllowed);
        Assert.Equal(4 * 60 * 60, result.MaxExtensionDurationSeconds);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_ActiveLease_AlreadyExtended_NotAllowed(
        SutProvider<GetCipherAccessStateQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId,
        AccessLease activeLease)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, Arg.Any<DateTime>())
            .Returns(activeLease);
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(orgId, collectionId, RequiresHumanApproval: false,
                [new HumanApprovalCondition()])
            {
                AllowsExtensions = true,
                MaxExtensionDurationSeconds = 2 * 60 * 60,
            });
        // A lease may be extended once; an existing extension means no more are allowed.
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CountExtensionsByLeaseIdAsync(activeLease.Id).Returns(1);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.False(result.ExtensionsAllowed);
        Assert.Equal(2 * 60 * 60, result.MaxExtensionDurationSeconds);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_ActiveLease_ExtensionsDisallowed_ReportsNotAllowed(
        SutProvider<GetCipherAccessStateQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId,
        AccessLease activeLease)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, Arg.Any<DateTime>())
            .Returns(activeLease);
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(orgId, collectionId, RequiresHumanApproval: false,
                [new HumanApprovalCondition()])
            {
                AllowsExtensions = false,
            });

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.False(result.ExtensionsAllowed);
        Assert.Null(result.MaxExtensionDurationSeconds);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CountExtensionsByLeaseIdAsync(default);
    }

    private static void SetupCipher(SutProvider<GetCipherAccessStateQuery> sutProvider, Guid userId, Guid cipherId)
    {
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns(new CipherDetails { Id = cipherId });
    }
}
