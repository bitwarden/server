using Bit.Core.Exceptions;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Enums;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.Models.Conditions;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Queries;

[SutProviderCustomize]
public class AccessPreCheckQueryTests
{
    [Theory, BitAutoData]
    public async Task PreCheckAsync_CipherNotAccessible_ThrowsNotFound(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId)
    {
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns((CipherDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PreCheckAsync(userId, cipherId));
    }

    [Theory, BitAutoData]
    public async Task PreCheckAsync_HumanApprovalCondition_ReturnsHuman(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(orgId, collectionId, RequiresHumanApproval: true,
                [new HumanApprovalCondition()]));

        var result = await sutProvider.Sut.PreCheckAsync(userId, cipherId);

        Assert.Equal(AccessApprovalMode.Human, result.ApprovalMode);
    }

    [Theory, BitAutoData]
    public async Task PreCheckAsync_AutoApproveRule_ReturnsAutomatic(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(orgId, collectionId, RequiresHumanApproval: false,
                [new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] }]));

        var result = await sutProvider.Sut.PreCheckAsync(userId, cipherId);

        Assert.Equal(AccessApprovalMode.Automatic, result.ApprovalMode);
    }

    [Theory, BitAutoData]
    public async Task PreCheckAsync_ExistingActiveLease_ReturnsHasActiveLease(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId, AccessLease activeLease)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, Arg.Any<DateTime>())
            .Returns(activeLease);

        var result = await sutProvider.Sut.PreCheckAsync(userId, cipherId);

        Assert.True(result.HasActiveLease);
        // The approval path is irrelevant once a lease is held, so the rule resolver is never consulted.
        await sutProvider.GetDependency<IGoverningRuleResolver>().DidNotReceiveWithAnyArgs().ResolveAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task PreCheckAsync_NotLeasingGated_ReturnsAutomatic(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns((GoverningRule?)null);

        var result = await sutProvider.Sut.PreCheckAsync(userId, cipherId);

        Assert.Equal(AccessApprovalMode.Automatic, result.ApprovalMode);
    }

    // PM-39858: the pre-check shapes the requester's duration picker, so it has to publish the same bounds submit
    // enforces. Publishing only the approval mode left the client offering its own hardcoded presets.
    [Theory, BitAutoData]
    public async Task PreCheckAsync_RuleWithDurationBounds_PublishesThem(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        SetupRule(sutProvider, userId, cipherId, orgId, collectionId,
            defaultLeaseDurationSeconds: 900, maxLeaseDurationSeconds: 1800);

        var result = await sutProvider.Sut.PreCheckAsync(userId, cipherId);

        Assert.Equal(900, result.DefaultDurationSeconds);
        Assert.Equal(1800, result.MaxDurationSeconds);
    }

    [Theory, BitAutoData]
    public async Task PreCheckAsync_RuleWithoutDurationBounds_PublishesGlobalBounds(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        SetupRule(sutProvider, userId, cipherId, orgId, collectionId,
            defaultLeaseDurationSeconds: null, maxLeaseDurationSeconds: null);

        var result = await sutProvider.Sut.PreCheckAsync(userId, cipherId);

        Assert.Equal(LeaseDurationBounds.GlobalDefaultSeconds, result.DefaultDurationSeconds);
        Assert.Equal(LeaseDurationBounds.GlobalMaxSeconds, result.MaxDurationSeconds);
    }

    [Theory, BitAutoData]
    public async Task PreCheckAsync_RuleDefaultAboveItsOwnMax_ClampsTheDefault(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        SetupRule(sutProvider, userId, cipherId, orgId, collectionId,
            defaultLeaseDurationSeconds: 3600, maxLeaseDurationSeconds: 900);

        var result = await sutProvider.Sut.PreCheckAsync(userId, cipherId);

        // Pre-filling the stored default would hand the requester a duration submit refuses.
        Assert.Equal(900, result.DefaultDurationSeconds);
        Assert.Equal(900, result.MaxDurationSeconds);
    }

    [Theory, BitAutoData]
    public async Task PreCheckAsync_RuleMaxAboveGlobalCeiling_PublishesTheGlobalCeiling(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        SetupRule(sutProvider, userId, cipherId, orgId, collectionId,
            defaultLeaseDurationSeconds: null, maxLeaseDurationSeconds: 7 * 24 * 60 * 60);

        var result = await sutProvider.Sut.PreCheckAsync(userId, cipherId);

        Assert.Equal(LeaseDurationBounds.GlobalMaxSeconds, result.MaxDurationSeconds);
    }

    [Theory, BitAutoData]
    public async Task PreCheckAsync_SingletonDoesNotBind_ReportsStartableWithoutQueryingTheCipher(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        SetupRule(sutProvider, userId, cipherId, orgId, collectionId, null, null);
        SetupSingleActiveLease(sutProvider, userId, cipherId, applies: false);

        var result = await sutProvider.Sut.PreCheckAsync(userId, cipherId);

        Assert.True(result.CanStartLease);
        Assert.Null(result.SlotFreesAt);
        // The short-circuit is the contract, not an optimization: an unconstrained caller must read as startable
        // however many leases are live, so the cipher must not even be consulted.
        await sutProvider.GetDependency<IAccessLeaseRepository>()
            .DidNotReceive()
            .GetActiveByCipherIdAsync(Arg.Any<Guid>(), Arg.Any<DateTime>());
    }

    [Theory, BitAutoData]
    public async Task PreCheckAsync_SingletonBindsAndSlotFree_ReportsStartable(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        SetupRule(sutProvider, userId, cipherId, orgId, collectionId, null, null);
        SetupSingleActiveLease(sutProvider, userId, cipherId, applies: true);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByCipherIdAsync(cipherId, Arg.Any<DateTime>())
            .Returns((AccessLease?)null);

        var result = await sutProvider.Sut.PreCheckAsync(userId, cipherId);

        Assert.True(result.CanStartLease);
        Assert.Null(result.SlotFreesAt);
    }

    [Theory, BitAutoData]
    public async Task PreCheckAsync_SingletonBindsAndAnotherMemberHoldsTheSlot_ReportsBlockedWithFreeTime(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId,
        AccessLease blockingLease)
    {
        SetupCipher(sutProvider, userId, cipherId);
        SetupRule(sutProvider, userId, cipherId, orgId, collectionId, null, null);
        SetupSingleActiveLease(sutProvider, userId, cipherId, applies: true);
        blockingLease.NotAfter = new DateTime(2026, 8, 31, 10, 52, 0, DateTimeKind.Utc);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByCipherIdAsync(cipherId, Arg.Any<DateTime>())
            .Returns(blockingLease);

        var result = await sutProvider.Sut.PreCheckAsync(userId, cipherId);

        Assert.False(result.CanStartLease);
        Assert.Equal(blockingLease.NotAfter, result.SlotFreesAt);
        // Nothing about the holder travels with the answer -- PM-42446 Alternative A.
        Assert.Equal(AccessApprovalMode.Automatic, result.ApprovalMode);
    }

    [Theory, BitAutoData]
    public async Task PreCheckAsync_ExistingActiveLease_ReportsStartableWithoutEvaluatingTheSingleton(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId, AccessLease activeLease)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, Arg.Any<DateTime>())
            .Returns(activeLease);

        var result = await sutProvider.Sut.PreCheckAsync(userId, cipherId);

        // The early return reveals the credential instead of rendering a form, so the field has nothing to qualify.
        Assert.True(result.CanStartLease);
        Assert.Null(result.SlotFreesAt);
        await sutProvider.GetDependency<ISingleActiveLeaseEvaluator>()
            .DidNotReceive()
            .AppliesAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    private static void SetupSingleActiveLease(SutProvider<AccessPreCheckQuery> sutProvider, Guid userId,
        Guid cipherId, bool applies)
    {
        sutProvider.GetDependency<ISingleActiveLeaseEvaluator>()
            .AppliesAsync(userId, cipherId)
            .Returns(applies);
    }

    private static void SetupCipher(SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId)
    {
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns(new CipherDetails { Id = cipherId });
    }

    private static void SetupRule(SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId,
        Guid orgId, Guid collectionId, int? defaultLeaseDurationSeconds, int? maxLeaseDurationSeconds)
    {
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(orgId, collectionId, RequiresHumanApproval: false,
                [new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] }])
            {
                DefaultLeaseDurationSeconds = defaultLeaseDurationSeconds,
                MaxLeaseDurationSeconds = maxLeaseDurationSeconds,
            });
    }
}
