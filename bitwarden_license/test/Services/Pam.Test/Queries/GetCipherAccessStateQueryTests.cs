using Bit.Core.Exceptions;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.Models.Conditions;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Queries;

[SutProviderCustomize]
public class GetCipherAccessStateQueryTests
{
    // Pinned far from the wall clock, so a derivation reading the real clock instead of TimeProvider fails loudly.
    private static readonly DateTime _now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetStateAsync_CipherNotAccessible_ThrowsNotFound(
        Guid userId, Guid cipherId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns((CipherDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetStateAsync(userId, cipherId));
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_NotGatedAndNothingHeld_ThrowsNotFound(
        Guid userId, Guid cipherId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        // No active lease, no pending request, and the resolver finds no governing rule.
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns((GoverningRule?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetStateAsync(userId, cipherId));
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_ActiveLease_ReturnsSnapshotWithLease(
        Guid userId, Guid cipherId, AccessLease activeLease)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(activeLease);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.Equal(cipherId, result.CipherId);
        Assert.Same(activeLease, result.ActiveLease);
        Assert.Null(result.PendingRequest);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_LeaseHeldButRuleRemoved_StillReturnsSnapshot(
        Guid userId, Guid cipherId, AccessLease activeLease)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(activeLease);
        // Access rule since removed; the held lease must still surface.
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns((GoverningRule?)null);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.Same(activeLease, result.ActiveLease);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_PendingRequest_MapsToDetails(
        Guid userId, Guid cipherId, AccessRequest pending)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        pending.CipherId = cipherId;
        pending.RequesterId = userId;
        pending.Action = AccessRequestAction.None;
        // The status derives against the query's clock; pin an open window so this reads as Pending.
        pending.NotAfter = _now.AddHours(1);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetActivePendingByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(pending);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.Null(result.ActiveLease);
        Assert.NotNull(result.PendingRequest);
        Assert.Equal(pending.Id, result.PendingRequest!.Id);
        Assert.Equal(pending.ExtensionOfLeaseId, result.PendingRequest.ExtensionOfLeaseId);
        Assert.Equal(AccessRequestStatus.Pending, result.PendingRequest.Status);
        // Pending has produced no lease and has no resolver yet.
        Assert.Null(result.PendingRequest.ProducedLeaseId);
        Assert.Empty(result.PendingRequest.Decisions);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_ApprovedRequest_MapsToDetails(
        Guid userId, Guid cipherId, AccessRequest approved)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        approved.CipherId = cipherId;
        approved.RequesterId = userId;
        approved.Action = AccessRequestAction.Approved;
        // The status derives against the query's clock; pin an open window so this reads as Approved.
        approved.NotAfter = _now.AddHours(1);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetActiveApprovedByRequesterIdCipherIdAsync(userId, cipherId, _now)
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
        // identity.
        Assert.Null(result.ApprovedRequest.ProducedLeaseId);
        Assert.Empty(result.ApprovedRequest.Decisions);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_ApprovedHeldButRuleRemoved_StillReturnsSnapshot(
        Guid userId, Guid cipherId, AccessRequest approved)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        approved.Action = AccessRequestAction.Approved;
        approved.NotAfter = _now.AddHours(1);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetActiveApprovedByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(approved);
        // Access rule since removed; the startable approval must still surface.
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns((GoverningRule?)null);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.NotNull(result.ApprovedRequest);
        Assert.Equal(approved.Id, result.ApprovedRequest!.Id);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_GatedButEmpty_ReturnsEmptySnapshot(
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
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
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId,
        AccessLease activeLease)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
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
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId,
        AccessLease activeLease)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(activeLease);
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(orgId, collectionId, RequiresHumanApproval: false,
                [new HumanApprovalCondition()])
            {
                AllowsExtensions = true,
                MaxExtensionDurationSeconds = 2 * 60 * 60,
            });
        // A lease extends only a single time; an existing extension blocks another.
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CountExtensionsByLeaseIdAsync(activeLease.Id).Returns(1);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.False(result.ExtensionsAllowed);
        Assert.Equal(2 * 60 * 60, result.MaxExtensionDurationSeconds);
    }

    [Theory, BitAutoData]
    public async Task GetStateAsync_ActiveLease_ExtensionsDisallowed_ReportsNotAllowed(
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId,
        AccessLease activeLease)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
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

    // PM-43689: an ungoverned path added after the lease was minted makes live resolution return null, which used
    // to hide the "Extend" control on a lease the extend call would still have accepted.
    [Theory, BitAutoData]
    public async Task GetStateAsync_UngovernedPathAddedSinceMint_StillReportsExtensionEligibility(
        Guid userId, Guid cipherId, Guid ruleId, AccessLease activeLease)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(activeLease);
        PinOriginatingRule(sutProvider, activeLease, ruleId, maxExtensionDurationSeconds: 3600);
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns((GoverningRule?)null);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CountExtensionsByLeaseIdAsync(activeLease.Id).Returns(0);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.True(result.ExtensionsAllowed);
        Assert.Equal(3600, result.MaxExtensionDurationSeconds);
    }

    // The cap the extend call will enforce is the pinned rule's, so that is the one the control must publish.
    [Theory, BitAutoData]
    public async Task GetStateAsync_ActiveLease_PublishesThePinnedRulesCap(
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId, Guid ruleId, AccessLease activeLease)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(activeLease);
        PinOriginatingRule(sutProvider, activeLease, ruleId, maxExtensionDurationSeconds: 3600);
        // A rule created or re-pointed since, with a longer cap; it governs the cipher today but not this lease.
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(orgId, collectionId, RequiresHumanApproval: false, [])
            {
                AllowsExtensions = true,
                MaxExtensionDurationSeconds = 8 * 60 * 60,
            });
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CountExtensionsByLeaseIdAsync(activeLease.Id).Returns(0);

        var result = await sutProvider.Sut.GetStateAsync(userId, cipherId);

        Assert.True(result.ExtensionsAllowed);
        Assert.Equal(3600, result.MaxExtensionDurationSeconds);
    }

    private static SutProvider<GetCipherAccessStateQuery> Setup()
    {
        var sutProvider = new SutProvider<GetCipherAccessStateQuery>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static void SetupCipher(SutProvider<GetCipherAccessStateQuery> sutProvider, Guid userId, Guid cipherId)
    {
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns(new CipherDetails { Id = cipherId });
    }

    // The rule the lease was granted under, reached through the request that birthed it.
    private static void PinOriginatingRule(
        SutProvider<GetCipherAccessStateQuery> sutProvider, AccessLease lease, Guid ruleId,
        int maxExtensionDurationSeconds)
    {
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetByIdAsync(lease.AccessRequestId)
            .Returns(new AccessRequest { Id = lease.AccessRequestId, RuleId = ruleId });

        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolvePinnedAsync(ruleId, lease.CollectionId)
            .Returns(new GoverningRule(lease.OrganizationId, lease.CollectionId, RequiresHumanApproval: false, [])
            {
                RuleId = ruleId,
                AllowsExtensions = true,
                MaxExtensionDurationSeconds = maxExtensionDurationSeconds,
            });
    }
}
