using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

[SutProviderCustomize]
public class AutomaticUserConfirmationPolicyEnforcementValidatorTests
{
    [Theory]
    [BitAutoData]
    public async Task IsCompliantAsync_WithPolicyEnabledOnSameOrganizationButNoOtherOrgs_ReturnsValid(
        SutProvider<AutomaticUserConfirmationPolicyEnforcementValidator> sutProvider,
        OrganizationUser organizationUser,
        User user)
    {
        // Arrange
        organizationUser.UserId = user.Id;

        var policyDetails = new PolicyDetails
        {
            OrganizationId = organizationUser.OrganizationId,
            PolicyType = PolicyType.AutomaticUserConfirmation
        };

        var request = new AutomaticUserConfirmationPolicyEnforcementRequest(
            organizationUser.OrganizationId,
            [organizationUser],
            user);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([policyDetails]));

        // Act
        var result = await sutProvider.Sut.IsCompliantAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task IsCompliantAsync_WithUserIsAMemberOfAProvider_ReturnsProviderUsersCannotJoinError(
        SutProvider<AutomaticUserConfirmationPolicyEnforcementValidator> sutProvider,
        OrganizationUser organizationUser,
        ProviderUser providerUser,
        User user)
    {
        // Arrange
        organizationUser.UserId = providerUser.UserId = user.Id;

        var policyDetails = new PolicyDetails
        {
            OrganizationId = organizationUser.OrganizationId,
            PolicyType = PolicyType.AutomaticUserConfirmation
        };

        var request = new AutomaticUserConfirmationPolicyEnforcementRequest(
            organizationUser.OrganizationId,
            [organizationUser],
            user);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([policyDetails]));

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns([providerUser]);

        // Act
        var result = await sutProvider.Sut.IsCompliantAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ProviderUsersCannotJoin>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task IsCompliantAsync_WithPolicyEnabledOnOtherOrganization_ReturnsOtherOrganizationDoesNotAllowOtherMembershipError(
        SutProvider<AutomaticUserConfirmationPolicyEnforcementValidator> sutProvider,
        OrganizationUser organizationUser,
        User user)
    {
        // Arrange
        organizationUser.UserId = user.Id;

        var otherOrgId = Guid.NewGuid();
        var policyDetails = new PolicyDetails
        {
            OrganizationId = otherOrgId, // Different from organizationUser.OrganizationId
            PolicyType = PolicyType.AutomaticUserConfirmation
        };

        var request = new AutomaticUserConfirmationPolicyEnforcementRequest(
            organizationUser.OrganizationId,
            [organizationUser],
            user);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([policyDetails]));

        // Act
        var result = await sutProvider.Sut.IsCompliantAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OtherOrganizationDoesNotAllowOtherMembership>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task IsCompliantAsync_UserIsAMemberOfAnotherOrgButNoPolicyDetailForAutoConfirm_ReturnsValid(
        SutProvider<AutomaticUserConfirmationPolicyEnforcementValidator> sutProvider,
        OrganizationUser organizationUser,
        OrganizationUser otherOrgUser,
        User user)
    {
        // Arrange
        // No policy enabled, so even with other org memberships, it should be valid
        organizationUser.UserId = user.Id;

        var request = new AutomaticUserConfirmationPolicyEnforcementRequest(
            organizationUser.OrganizationId,
            [organizationUser, otherOrgUser],
            user);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([])); // no policy details

        // Act
        var result = await sutProvider.Sut.IsCompliantAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task IsCompliantAsync_ChecksConditionsInCorrectOrder_ReturnsFirstFailure(
        SutProvider<AutomaticUserConfirmationPolicyEnforcementValidator> sutProvider,
        OrganizationUser organizationUser,
        OrganizationUser otherOrgUser,
        ProviderUser providerUser,
        User user)
    {
        // Arrange - Set up conditions that would fail multiple checks
        var policyDetails = new PolicyDetails
        {
            OrganizationId = organizationUser.OrganizationId,
            PolicyType = PolicyType.AutomaticUserConfirmation,
            OrganizationUserId = organizationUser.Id
        };

        var request = new AutomaticUserConfirmationPolicyEnforcementRequest(
            organizationUser.OrganizationId,
            [organizationUser, otherOrgUser],
            user);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([policyDetails]));

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns([providerUser]);

        // Act
        var result = await sutProvider.Sut.IsCompliantAsync(request);

        // Assert - Should fail on the FIRST check Org user does not match request object
        Assert.True(result.IsError);
        Assert.IsType<CurrentOrganizationUserIsNotPresentInRequest>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task IsCompliantAsync_WithNoOtherOrganizationsAndNotAProvider_ReturnsValid(
        SutProvider<AutomaticUserConfirmationPolicyEnforcementValidator> sutProvider,
        OrganizationUser organizationUser,
        User user)
    {
        // Arrange
        organizationUser.UserId = user.Id;

        var request = new AutomaticUserConfirmationPolicyEnforcementRequest(
            organizationUser.OrganizationId,
            [organizationUser],
            user);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([
                new PolicyDetails
                {
                    OrganizationUserId = organizationUser.Id,
                    OrganizationId = organizationUser.OrganizationId,
                    PolicyType = PolicyType.AutomaticUserConfirmation,
                }
            ]));

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.IsCompliantAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }
}
