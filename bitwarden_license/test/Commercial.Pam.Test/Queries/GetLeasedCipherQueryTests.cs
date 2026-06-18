using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Engine;
using Bit.Pam.Entities;
using Bit.Pam.Models;
using Bit.Pam.Models.Conditions;
using Bit.Commercial.Pam.OrganizationFeatures.Queries;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Pam.Test.Queries;

[SutProviderCustomize]
public class GetLeasedCipherQueryTests
{
    private static readonly DateTime _now = new(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetLeasedCipherAsync_NoActiveLease_ReturnsNull(Guid userId, Guid cipherId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns((AccessLease?)null);

        var result = await sutProvider.Sut.GetLeasedCipherAsync(userId, cipherId);

        Assert.Null(result);
        // Accessibility is never consulted when there is no lease.
        await sutProvider.GetDependency<ICipherRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetLeasedCipherAsync_ActiveLeaseButCipherNotAccessible_ReturnsNull(
        Guid userId, Guid cipherId, AccessLease lease)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(lease);
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns((CipherDetails?)null);

        var result = await sutProvider.Sut.GetLeasedCipherAsync(userId, cipherId);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetLeasedCipherAsync_ActiveLeaseAndAccessible_ReturnsCipher(
        Guid userId, Guid cipherId, AccessLease lease)
    {
        var sutProvider = Setup();
        var cipher = new CipherDetails { Id = cipherId, Data = "2.iv|ct|mac" };
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(lease);
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns(cipher);

        var result = await sutProvider.Sut.GetLeasedCipherAsync(userId, cipherId);

        Assert.NotNull(result);
        Assert.Equal(cipherId, result!.Id);
        Assert.Equal("2.iv|ct|mac", result.Data);
        // The active-lease lookup uses the TimeProvider's now.
        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now);
    }

    [Theory, BitAutoData]
    public async Task GetLeasedCipherAsync_ConditionsDeny_WithholdsDataAndReturnsNull(
        Guid userId, Guid cipherId, AccessLease lease, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(lease);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId);
        SetupEvaluation(sutProvider, AccessEvaluation.Deny(DenyReason.NotWithinIpRange));

        var result = await sutProvider.Sut.GetLeasedCipherAsync(userId, cipherId);

        Assert.Null(result);
        // A denied evaluation must withhold the data: the cipher is never read.
        await sutProvider.GetDependency<ICipherRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetLeasedCipherAsync_ConditionsAllow_ReturnsCipher(
        Guid userId, Guid cipherId, AccessLease lease, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        var cipher = new CipherDetails { Id = cipherId };
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(lease);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherId, userId).Returns(cipher);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId);
        SetupEvaluation(sutProvider, AccessEvaluation.Allow);

        var result = await sutProvider.Sut.GetLeasedCipherAsync(userId, cipherId);

        Assert.Same(cipher, result);
    }

    [Theory, BitAutoData]
    public async Task GetLeasedCipherAsync_ConditionsRequireApproval_StillReturnsCipher(
        Guid userId, Guid cipherId, AccessLease lease, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        var cipher = new CipherDetails { Id = cipherId };
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(lease);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherId, userId).Returns(cipher);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId);
        // Holding the lease is proof approval was already granted, so a deferred-approval outcome must not re-gate.
        SetupEvaluation(sutProvider, AccessEvaluation.RequiresApproval);

        var result = await sutProvider.Sut.GetLeasedCipherAsync(userId, cipherId);

        Assert.Same(cipher, result);
    }

    private static SutProvider<GetLeasedCipherQuery> Setup()
    {
        var sutProvider = new SutProvider<GetLeasedCipherQuery>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static void SetupResolution(SutProvider<GetLeasedCipherQuery> sutProvider, Guid userId, Guid cipherId,
        Guid orgId, Guid collectionId)
    {
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(orgId, collectionId, false,
                [new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] }]));
    }

    private static void SetupEvaluation(SutProvider<GetLeasedCipherQuery> sutProvider, AccessEvaluation decision)
    {
        sutProvider.GetDependency<IAccessRuleEngine>()
            .Evaluate(Arg.Any<IReadOnlyList<AccessCondition>>(), Arg.Any<AccessSignals>())
            .Returns(decision);
    }
}
