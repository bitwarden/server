using System.Text.Json;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Test.Utilities;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;


// Note: test names follow MethodName_StateUnderTest_ExpectedBehavior pattern.
[ControllerCustomize(typeof(PoliciesController))]
[SutProviderCustomize]
public class PoliciesControllerTests
{
    [Fact]
    public void AllActionMethodsHaveAuthorization()
    {
        ControllerAuthorizationTestHelpers.AssertAllHttpMethodsHaveAuthorization(
            typeof(PoliciesController));
    }

    [Theory]
    [BitAutoData]
    public async Task GetMasterPasswordPolicy_WhenCalled_ReturnsMasterPasswordPolicy(
        SutProvider<PoliciesController> sutProvider,
        Guid orgId,
        Policy policy,
        MasterPasswordPolicyData mpPolicyData,
        OrganizationAbility organizationAbility)
    {
        // Arrange
        organizationAbility.UsePolicies = true;

        var organizationAbilityCacheService = sutProvider.GetDependency<IOrganizationAbilityCacheService>();
        organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId).Returns(organizationAbility);

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

        // Assert that Data is passed through as the raw JSON string, and still contains
        // all the expected MasterPasswordPolicyData properties.
        Assert.Equal(policy.Data, result.Data);

        using var dataDocument = JsonDocument.Parse(result.Data);
        var dataElement = dataDocument.RootElement;
        Assert.Equal(mpPolicyData.MinComplexity, dataElement.GetProperty("minComplexity").GetInt32());
        Assert.Equal(mpPolicyData.MinLength, dataElement.GetProperty("minLength").GetInt32());
        Assert.Equal(mpPolicyData.RequireLower, dataElement.GetProperty("requireLower").GetBoolean());
        Assert.Equal(mpPolicyData.RequireUpper, dataElement.GetProperty("requireUpper").GetBoolean());
        Assert.Equal(mpPolicyData.RequireNumbers, dataElement.GetProperty("requireNumbers").GetBoolean());
        Assert.Equal(mpPolicyData.RequireSpecial, dataElement.GetProperty("requireSpecial").GetBoolean());
        Assert.Equal(mpPolicyData.EnforceOnLogin, dataElement.GetProperty("enforceOnLogin").GetBoolean());
    }


    [Theory]
    [BitAutoData]
    public async Task GetMasterPasswordPolicy_OrgUserIsNull_ThrowsNotFoundException(
        SutProvider<PoliciesController> sutProvider, Guid orgId)
    {
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetMasterPasswordPolicy(orgId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetMasterPasswordPolicy_PolicyIsNull_ThrowsNotFoundException(
        SutProvider<PoliciesController> sutProvider, Guid orgId)
    {
        // Arrange
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(orgId, PolicyType.MasterPassword)
            .Returns((Policy)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetMasterPasswordPolicy(orgId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetMasterPasswordPolicy_PolicyNotEnabled_ThrowsNotFoundException(
        SutProvider<PoliciesController> sutProvider, Guid orgId, Policy policy)
    {
        // Arrange
        policy.Enabled = false; // Ensuring the policy is not enabled
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(orgId, PolicyType.MasterPassword)
            .Returns(policy);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetMasterPasswordPolicy(orgId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetMasterPasswordPolicy_WhenUsePoliciesIsFalse_ThrowsNotFoundException(
        SutProvider<PoliciesController> sutProvider,
        Guid orgId)
    {
        // Arrange
        var organizationAbilityCacheService = sutProvider.GetDependency<IOrganizationAbilityCacheService>();
        organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId).Returns((OrganizationAbility)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetMasterPasswordPolicy(orgId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetMasterPasswordPolicy_WhenOrgIsNull_ThrowsNotFoundException(
        SutProvider<PoliciesController> sutProvider,
        Guid orgId,
        OrganizationAbility organizationAbility)
    {
        // Arrange
        organizationAbility.UsePolicies = false;

        var organizationAbilityCacheService = sutProvider.GetDependency<IOrganizationAbilityCacheService>();
        organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId).Returns(organizationAbility);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetMasterPasswordPolicy(orgId));
    }

    [Theory]
    [BitAutoData]
    public async Task Get_WhenUserCanManagePolicies_WithExistingType_ReturnsExistingPolicy(
        SutProvider<PoliciesController> sutProvider, Guid orgId, PolicyStatus policy, PolicyType type)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(orgId)
            .Returns(true);

        policy.Type = type;
        policy.Enabled = true;
        policy.Data = null;

        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(orgId, type)
            .Returns(policy);

        // Act
        var result = await sutProvider.Sut.Get(orgId, type);

        // Assert
        Assert.IsType<PolicyStatusResponseModel>(result);
        Assert.Equal(policy.Type, result.Type);
        Assert.Equal(policy.Enabled, result.Enabled);
        Assert.Equal(policy.OrganizationId, result.OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task GetByToken_WhenOrganizationUseUsePoliciesIsFalse_ThrowsNotFoundException(
        SutProvider<PoliciesController> sutProvider, Guid orgId, Guid organizationUserId, string token, string email,
        OrganizationAbility organizationAbility)
    {
        // Arrange
        organizationAbility.UsePolicies = false;

        var organizationAbilityCacheService = sutProvider.GetDependency<IOrganizationAbilityCacheService>();
        organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId).Returns(organizationAbility);


        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetByToken(orgId, email, token, organizationUserId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetByToken_WhenOrganizationIsNull_ThrowsNotFoundException(
        SutProvider<PoliciesController> sutProvider, Guid orgId, Guid organizationUserId, string token, string email)
    {
        // Arrange
        var organizationAbilityCacheService = sutProvider.GetDependency<IOrganizationAbilityCacheService>();
        organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId).Returns((OrganizationAbility)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetByToken(orgId, email, token, organizationUserId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetByToken_WhenTokenIsInvalid_ThrowsNotFoundException(
        SutProvider<PoliciesController> sutProvider,
        Guid orgId,
        Guid organizationUserId,
        string token,
        string email,
        OrganizationAbility organizationAbility
    )
    {
        // Arrange
        organizationAbility.UsePolicies = true;

        var organizationAbilityCacheService = sutProvider.GetDependency<IOrganizationAbilityCacheService>();
        organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId).Returns(organizationAbility);

        var decryptedToken = Substitute.For<OrgUserInviteTokenable>();
        decryptedToken.Valid.Returns(false);

        var orgUserInviteTokenDataFactory =
            sutProvider.GetDependency<IDataProtectorTokenFactory<OrgUserInviteTokenable>>();

        orgUserInviteTokenDataFactory.TryUnprotect(token, out Arg.Any<OrgUserInviteTokenable>())
            .Returns(x =>
            {
                x[1] = decryptedToken;
                return true;
            });

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetByToken(orgId, email, token, organizationUserId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetByToken_WhenUserIsNull_ThrowsNotFoundException(
        SutProvider<PoliciesController> sutProvider,
        Guid orgId,
        Guid organizationUserId,
        string token,
        string email,
        OrganizationAbility organizationAbility
    )
    {
        // Arrange
        organizationAbility.UsePolicies = true;

        var organizationAbilityCacheService = sutProvider.GetDependency<IOrganizationAbilityCacheService>();
        organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId).Returns(organizationAbility);

        var decryptedToken = Substitute.For<OrgUserInviteTokenable>();
        decryptedToken.Valid.Returns(true);
        decryptedToken.OrgUserId = organizationUserId;
        decryptedToken.OrgUserEmail = email;

        var orgUserInviteTokenDataFactory =
            sutProvider.GetDependency<IDataProtectorTokenFactory<OrgUserInviteTokenable>>();

        orgUserInviteTokenDataFactory.TryUnprotect(token, out Arg.Any<OrgUserInviteTokenable>())
            .Returns(x =>
            {
                x[1] = decryptedToken;
                return true;
            });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns((OrganizationUser)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetByToken(orgId, email, token, organizationUserId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetByToken_WhenUserOrgIdDoesNotMatchOrgId_ThrowsNotFoundException(
        SutProvider<PoliciesController> sutProvider,
        Guid orgId,
        Guid organizationUserId,
        string token,
        string email,
        OrganizationUser orgUser,
        OrganizationAbility organizationAbility
    )
    {
        // Arrange
        organizationAbility.UsePolicies = true;

        var organizationAbilityCacheService = sutProvider.GetDependency<IOrganizationAbilityCacheService>();
        organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId).Returns(organizationAbility);

        var decryptedToken = Substitute.For<OrgUserInviteTokenable>();
        decryptedToken.Valid.Returns(true);
        decryptedToken.OrgUserId = organizationUserId;
        decryptedToken.OrgUserEmail = email;

        var orgUserInviteTokenDataFactory =
            sutProvider.GetDependency<IDataProtectorTokenFactory<OrgUserInviteTokenable>>();

        orgUserInviteTokenDataFactory.TryUnprotect(token, out Arg.Any<OrgUserInviteTokenable>())
            .Returns(x =>
            {
                x[1] = decryptedToken;
                return true;
            });

        orgUser.OrganizationId = Guid.Empty;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(orgUser);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.GetByToken(orgId, email, token, organizationUserId));
    }

    [Theory]
    [BitAutoData]
    public async Task GetByToken_ShouldReturnEnabledPolicies(
        SutProvider<PoliciesController> sutProvider,
        Guid orgId,
        Guid organizationUserId,
        string token,
        string email,
        OrganizationUser orgUser,
        OrganizationAbility organizationAbility
    )
    {
        // Arrange
        organizationAbility.UsePolicies = true;

        var organizationAbilityCacheService = sutProvider.GetDependency<IOrganizationAbilityCacheService>();
        organizationAbilityCacheService.GetOrganizationAbilityAsync(orgId).Returns(organizationAbility);

        var decryptedToken = Substitute.For<OrgUserInviteTokenable>();
        decryptedToken.Valid.Returns(true);
        decryptedToken.OrgUserId = organizationUserId;
        decryptedToken.OrgUserEmail = email;

        var orgUserInviteTokenDataFactory =
            sutProvider.GetDependency<IDataProtectorTokenFactory<OrgUserInviteTokenable>>();

        orgUserInviteTokenDataFactory.TryUnprotect(token, out Arg.Any<OrgUserInviteTokenable>())
            .Returns(x =>
            {
                x[1] = decryptedToken;
                return true;
            });

        orgUser.OrganizationId = orgId;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUserId)
            .Returns(orgUser);

        var enabledPolicy = Substitute.For<Policy>();
        enabledPolicy.Enabled = true;
        var disabledPolicy = Substitute.For<Policy>();
        disabledPolicy.Enabled = false;

        var policies = new[] { enabledPolicy, disabledPolicy };


        sutProvider.GetDependency<IPolicyRepository>()
            .GetManyByOrganizationIdAsync(orgId)
            .Returns(policies);

        // Act
        var result = await sutProvider.Sut.GetByToken(orgId, email, token, organizationUserId);

        // Assert
        var expectedPolicy = result.Data.Single();

        Assert.NotNull(result);

        Assert.Equal(enabledPolicy.Id, expectedPolicy.Id);
        Assert.Equal(enabledPolicy.Type, expectedPolicy.Type);
        Assert.Equal(enabledPolicy.Enabled, expectedPolicy.Enabled);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_SavesPolicyWithCorrectArguments(
        SutProvider<PoliciesController> sutProvider, Guid orgId,
        SavePolicyRequest model, Policy policy, Guid userId)
    {
        // Arrange
        policy.Data = null;

        sutProvider.GetDependency<ICurrentContext>()
            .UserId
            .Returns(userId);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(orgId)
            .Returns(true);

        sutProvider.GetDependency<ISavePolicyCommand>()
            .SaveAsync(Arg.Any<SavePolicyModel>())
            .Returns(policy);

        // Act
        var result = await sutProvider.Sut.Put(orgId, policy.Type, model);

        // Assert
        await sutProvider.GetDependency<ISavePolicyCommand>()
            .Received(1)
            .SaveAsync(Arg.Is<SavePolicyModel>(m => m.PolicyUpdate.OrganizationId == orgId &&
                                                    m.PolicyUpdate.Type == policy.Type &&
                                                    m.PolicyUpdate.Enabled == model.Policy.Enabled &&
                                                    m.PerformedBy.UserId == userId &&
                                                    m.PerformedBy.IsOrganizationOwnerOrProvider == true));

        Assert.NotNull(result);
        Assert.Equal(policy.Id, result.Id);
        Assert.Equal(policy.Type, result.Type);
    }
}
