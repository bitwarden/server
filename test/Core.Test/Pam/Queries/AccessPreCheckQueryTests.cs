using Bit.Core.Exceptions;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.Models.Rules;
using Bit.Core.Pam.OrganizationFeatures.Queries;
using Bit.Core.Pam.Services;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Queries;

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
    public async Task PreCheckAsync_HumanApprovalRule_ReturnsHuman(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessApprovalResolver>()
            .ResolveAsync(userId, cipherId)
            .Returns(new AccessApprovalResolution(orgId, collectionId, RequiresHumanApproval: true, new HumanApprovalRule()));

        var result = await sutProvider.Sut.PreCheckAsync(userId, cipherId);

        Assert.Equal(AccessApprovalOutcome.Human, result.Outcome);
    }

    [Theory, BitAutoData]
    public async Task PreCheckAsync_AutoApproveRule_ReturnsAutomatic(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessApprovalResolver>()
            .ResolveAsync(userId, cipherId)
            .Returns(new AccessApprovalResolution(orgId, collectionId, RequiresHumanApproval: false, new IpAllowlistRule { Cidrs = ["10.0.0.0/8"] }));

        var result = await sutProvider.Sut.PreCheckAsync(userId, cipherId);

        Assert.Equal(AccessApprovalOutcome.Automatic, result.Outcome);
    }

    [Theory, BitAutoData]
    public async Task PreCheckAsync_NotLeasingGated_ReturnsAutomatic(
        SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId)
    {
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessApprovalResolver>()
            .ResolveAsync(userId, cipherId)
            .Returns((AccessApprovalResolution?)null);

        var result = await sutProvider.Sut.PreCheckAsync(userId, cipherId);

        Assert.Equal(AccessApprovalOutcome.Automatic, result.Outcome);
    }

    private static void SetupCipher(SutProvider<AccessPreCheckQuery> sutProvider, Guid userId, Guid cipherId)
    {
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns(new CipherDetails { Id = cipherId });
    }
}
