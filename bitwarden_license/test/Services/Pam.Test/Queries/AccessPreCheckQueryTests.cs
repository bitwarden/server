using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Enums;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.Models.Conditions;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Services.Pam.Services;
using Bit.Core.Exceptions;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
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

    private static void SetupCipher(SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId)
    {
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns(new CipherDetails { Id = cipherId });
    }
}
