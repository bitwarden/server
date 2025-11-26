using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;

[SutProviderCustomize]
public class AutomaticUserConfirmationPolicyEnforcementQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task IsCompliantAsync_WithNoOtherOrganizations_ReturnsValid(
        SutProvider<AutomaticUserConfirmationPolicyEnforcementQuery> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user)
    {
        // Arrange
        var request = new AutomaticUserConfirmationPolicyEnforcementRequest(
            organizationUser,
            [],
            user);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        // Act
        var result = await sutProvider.Sut.IsCompliantAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task IsCompliantAsync_WithPolicyEnabledOnSameOrganizationButNoOtherOrgs_ReturnsValid(
        SutProvider<AutomaticUserConfirmationPolicyEnforcementQuery> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user)
    {
        // Arrange
        var policyDetails = new PolicyDetails
        {
            OrganizationId = organizationUser.OrganizationId,
            PolicyType = PolicyType.AutomaticUserConfirmation,
            IsProvider = false
        };

        var request = new AutomaticUserConfirmationPolicyEnforcementRequest(
            organizationUser,
            [],
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
    public async Task IsCompliantAsync_WithPolicyEnabledOnSameOrgAndUserHasOtherOrgs_ReturnsOrganizationEnforcesSingleOrgPolicyError(
        SutProvider<AutomaticUserConfirmationPolicyEnforcementQuery> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        OrganizationUser otherOrgUser,
        User user)
    {
        // Arrange
        var policyDetails = new PolicyDetails
        {
            OrganizationId = organizationUser.OrganizationId,
            PolicyType = PolicyType.AutomaticUserConfirmation,
            IsProvider = false
        };

        var request = new AutomaticUserConfirmationPolicyEnforcementRequest(
            organizationUser,
            [otherOrgUser], // User has other org memberships
            user);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([policyDetails]));

        // Act
        var result = await sutProvider.Sut.IsCompliantAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationEnforcesSingleOrgPolicy>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task IsCompliantAsync_WithUserIsProvider_ReturnsProviderUsersCannotJoinError(
        SutProvider<AutomaticUserConfirmationPolicyEnforcementQuery> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user)
    {
        // Arrange
        var policyDetails = new PolicyDetails
        {
            OrganizationId = organizationUser.OrganizationId,
            PolicyType = PolicyType.AutomaticUserConfirmation,
            IsProvider = true
        };

        var request = new AutomaticUserConfirmationPolicyEnforcementRequest(
            organizationUser,
            [],
            user);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([policyDetails]));

        // Act
        var result = await sutProvider.Sut.IsCompliantAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<ProviderUsersCannotJoin>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task IsCompliantAsync_WithPolicyEnabledOnOtherOrganization_ReturnsOtherOrganizationEnforcesSingleOrgPolicyError(
        SutProvider<AutomaticUserConfirmationPolicyEnforcementQuery> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user)
    {
        // Arrange
        var otherOrgId = Guid.NewGuid();
        var policyDetails = new PolicyDetails
        {
            OrganizationId = otherOrgId, // Different from organizationUser.OrganizationId
            PolicyType = PolicyType.AutomaticUserConfirmation,
            IsProvider = false
        };

        var request = new AutomaticUserConfirmationPolicyEnforcementRequest(
            organizationUser,
            [],
            user);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([policyDetails]));

        // Act
        var result = await sutProvider.Sut.IsCompliantAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OtherOrganizationEnforcesSingleOrgPolicy>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task IsCompliantAsync_WithOtherOrganizationsButNoPolicyEnabled_ReturnsValid(
        SutProvider<AutomaticUserConfirmationPolicyEnforcementQuery> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        OrganizationUser otherOrgUser,
        User user)
    {
        // Arrange
        // No policy enabled, so even with other org memberships, it should be valid
        var request = new AutomaticUserConfirmationPolicyEnforcementRequest(
            organizationUser,
            [otherOrgUser], // User has other organization memberships
            user);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        // Act
        var result = await sutProvider.Sut.IsCompliantAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task IsCompliantAsync_WithEmptyOtherOrganizationsAndSingleOrg_ReturnsValid(
        SutProvider<AutomaticUserConfirmationPolicyEnforcementQuery> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user)
    {
        // Arrange
        var request = new AutomaticUserConfirmationPolicyEnforcementRequest(
            organizationUser,
            [organizationUser],
            user);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        // Act
        var result = await sutProvider.Sut.IsCompliantAsync(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(request, result.Request);
    }

    [Theory]
    [BitAutoData]
    public async Task IsCompliantAsync_ChecksConditionsInCorrectOrder_ReturnsFirstFailure(
        SutProvider<AutomaticUserConfirmationPolicyEnforcementQuery> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        OrganizationUser otherOrgUser,
        User user)
    {
        // Arrange - Set up conditions that would fail multiple checks
        var policyDetails = new PolicyDetails
        {
            OrganizationId = organizationUser.OrganizationId, // Would trigger first check
            PolicyType = PolicyType.AutomaticUserConfirmation,
            IsProvider = true // Would also trigger second check if first passes
        };

        var request = new AutomaticUserConfirmationPolicyEnforcementRequest(
            organizationUser,
            [otherOrgUser], // Would also fail the last check
            user);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([policyDetails]));

        // Act
        var result = await sutProvider.Sut.IsCompliantAsync(request);

        // Assert - Should fail on the FIRST check (IsEnabled on same org AND has other orgs)
        Assert.True(result.IsError);
        Assert.IsType<OrganizationEnforcesSingleOrgPolicy>(result.AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task IsCompliantAsync_WithNullOtherOrganizations_ReturnsValidWhenNoOtherOrgs(
        SutProvider<AutomaticUserConfirmationPolicyEnforcementQuery> sutProvider,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser organizationUser,
        User user)
    {
        // Arrange
        var request = new AutomaticUserConfirmationPolicyEnforcementRequest(
            organizationUser,
            null, // Null other organizations
            user);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        // Act
        var result = await sutProvider.Sut.IsCompliantAsync(request);

        // Assert
        Assert.True(result.IsValid);
    }
}
