using System.Security.Claims;
using System.Text.Json;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers;


// Note: test names follow MethodName_StateUnderTest_ExpectedBehavior pattern.
[ControllerCustomize(typeof(PoliciesController))]
[SutProviderCustomize]
public class PoliciesControllerTests
{

    [Theory]
    [BitAutoData]
    public async Task GetMasterPasswordPolicy_WhenCalled_ReturnsMasterPasswordPolicy(
        SutProvider<PoliciesController> sutProvider, Guid orgId, Guid userId, OrganizationUser orgUser,
        Policy policy, MasterPasswordPolicyData mpPolicyData)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns((Guid?)userId);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(orgId, userId)
            .Returns(orgUser);


        policy.Type = PolicyType.MasterPassword;
        policy.Enabled = true;
        // data should be a JSON serialized version of the mpPolicyData object
        policy.Data = JsonSerializer.Serialize(mpPolicyData);

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(orgId, PolicyType.MasterPassword)
            .Returns(policy);

        // Act
        var result = await sutProvider.Sut.GetMasterPasswordPolicy(orgId);

        // Assert

        Assert.NotNull(result);
        Assert.Equal(policy.Id, result.Id);
        Assert.Equal(policy.Type, result.Type);
        Assert.Equal(policy.Enabled, result.Enabled);

        // Assert that the data is deserialized correctly into a Dictionary<string, object>
        // for all MasterPasswordPolicyData properties
        Assert.Equal(mpPolicyData.MinComplexity, ((JsonElement)result.Data["MinComplexity"]).GetInt32());
        Assert.Equal(mpPolicyData.MinLength, ((JsonElement)result.Data["MinLength"]).GetInt32());
        Assert.Equal(mpPolicyData.RequireLower, ((JsonElement)result.Data["RequireLower"]).GetBoolean());
        Assert.Equal(mpPolicyData.RequireUpper, ((JsonElement)result.Data["RequireUpper"]).GetBoolean());
        Assert.Equal(mpPolicyData.RequireNumbers, ((JsonElement)result.Data["RequireNumbers"]).GetBoolean());
        Assert.Equal(mpPolicyData.RequireSpecial, ((JsonElement)result.Data["RequireSpecial"]).GetBoolean());
        Assert.Equal(mpPolicyData.EnforceOnLogin, ((JsonElement)result.Data["EnforceOnLogin"]).GetBoolean());
    }


    [Theory]
    [BitAutoData]
    public async Task GetMasterPasswordPolicy_OrgUserIsNull_ThrowsNotFoundException(
        SutProvider<PoliciesController> sutProvider, Guid orgId, Guid userId)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns((Guid?)userId);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(orgId, userId)
            .Returns((OrganizationUser)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetMasterPasswordPolicy(orgId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetMasterPasswordPolicy_PolicyIsNull_ThrowsNotFoundException(
        SutProvider<PoliciesController> sutProvider, Guid orgId, Guid userId, OrganizationUser orgUser)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns((Guid?)userId);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(orgId, userId)
            .Returns(orgUser);

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(orgId, PolicyType.MasterPassword)
            .Returns((Policy)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetMasterPasswordPolicy(orgId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetMasterPasswordPolicy_PolicyNotEnabled_ThrowsNotFoundException(
        SutProvider<PoliciesController> sutProvider, Guid orgId, Guid userId, OrganizationUser orgUser, Policy policy)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns((Guid?)userId);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(orgId, userId)
            .Returns(orgUser);

        policy.Enabled = false; // Ensuring the policy is not enabled
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(orgId, PolicyType.MasterPassword)
            .Returns(policy);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetMasterPasswordPolicy(orgId));
    }

    [Theory]
    [BitAutoData]
    public async Task Get_WhenUserCanManagePolicies_WithExistingType_ReturnsExistingPolicy(
        SutProvider<PoliciesController> sutProvider, Guid orgId, Policy policy, int type)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
        .ManagePolicies(orgId)
        .Returns(true);

        policy.Type = (PolicyType)type;
        policy.Enabled = true;
        policy.Data = null;

        sutProvider.GetDependency<IPolicyRepository>()
        .GetByOrganizationIdTypeAsync(orgId, (PolicyType)type)
        .Returns(policy);

        // Act
        var result = await sutProvider.Sut.Get(orgId, type);

        // Assert
        Assert.IsType<PolicyDetailResponseModel>(result);
        Assert.Equal(policy.Id, result.Id);
        Assert.Equal(policy.Type, result.Type);
        Assert.Equal(policy.Enabled, result.Enabled);
        Assert.Equal(policy.OrganizationId, result.OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task Get_WhenUserCanManagePolicies_WithNonExistingType_ReturnsDefaultPolicy(
        SutProvider<PoliciesController> sutProvider, Guid orgId, int type)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
        .ManagePolicies(orgId)
        .Returns(true);

        sutProvider.GetDependency<IPolicyRepository>()
        .GetByOrganizationIdTypeAsync(orgId, (PolicyType)type)
        .Returns((Policy)null);

        // Act
        var result = await sutProvider.Sut.Get(orgId, type);

        // Assert
        Assert.IsType<PolicyDetailResponseModel>(result);
        Assert.Equal(result.Type, (PolicyType)type);
        Assert.False(result.Enabled);
    }

    [Theory]
    [BitAutoData]
    public async Task Get_WhenUserCannotManagePolicies_ThrowsNotFoundException(
        SutProvider<PoliciesController> sutProvider, Guid orgId, int type)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
        .ManagePolicies(orgId)
        .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Get(orgId, type));
    }

}
