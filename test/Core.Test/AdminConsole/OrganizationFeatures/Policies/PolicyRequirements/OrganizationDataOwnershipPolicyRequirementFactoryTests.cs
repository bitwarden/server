using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Enums;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

[SutProviderCustomize]
public class OrganizationDataOwnershipPolicyRequirementFactoryTests
{
    [Theory, BitAutoData]
    public void State_WithNoPolicies_ReturnsAllowed(SutProvider<OrganizationDataOwnershipPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.Equal(OrganizationDataOwnershipState.Disabled, actual.State);
    }

    [Theory, BitAutoData]
    public void State_WithOrganizationDataOwnershipPolicies_ReturnsRestricted(
        [PolicyDetails(PolicyType.OrganizationDataOwnership)] PolicyDetails[] policies,
        SutProvider<OrganizationDataOwnershipPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(policies);

        Assert.Equal(OrganizationDataOwnershipState.Enabled, actual.State);
    }

    [Theory, BitAutoData]
    public void PolicyType_ReturnsOrganizationDataOwnership(SutProvider<OrganizationDataOwnershipPolicyRequirementFactory> sutProvider)
    {
        Assert.Equal(PolicyType.OrganizationDataOwnership, sutProvider.Sut.PolicyType);
    }

    [Theory, BitAutoData]
    public void GetDefaultCollectionRequest_WithConfirmedUser_ReturnsTrue(
    [PolicyDetails(PolicyType.OrganizationDataOwnership, userStatus: OrganizationUserStatusType.Confirmed)] PolicyDetails[] policies,
    SutProvider<OrganizationDataOwnershipPolicyRequirementFactory> sutProvider)
    {
        // Arrange
        var requirement = sutProvider.Sut.Create(policies);
        var expectedOrganizationUserId = policies[0].OrganizationUserId;
        var organizationId = policies[0].OrganizationId;

        // Act
        var result = requirement.GetDefaultCollectionRequest(organizationId);

        // Assert
        Assert.Equal(expectedOrganizationUserId, result.OrganizationUserId);
        Assert.True(result.ShouldCreateDefaultCollection);
    }

    [Theory, BitAutoData]
    public void GetDefaultCollectionRequest_WithAcceptedUser_ReturnsFalse(
        [PolicyDetails(PolicyType.OrganizationDataOwnership, userStatus: OrganizationUserStatusType.Accepted)] PolicyDetails[] policies,
        SutProvider<OrganizationDataOwnershipPolicyRequirementFactory> sutProvider)
    {
        // Arrange
        var requirement = sutProvider.Sut.Create(policies);
        var organizationId = policies[0].OrganizationId;

        // Act
        var result = requirement.GetDefaultCollectionRequest(organizationId);

        // Assert
        Assert.Equal(Guid.Empty, result.OrganizationUserId);
        Assert.False(result.ShouldCreateDefaultCollection);
    }

    [Theory, BitAutoData]
    public void GetDefaultCollectionRequest_WithNoPolicies_ReturnsFalse(
        SutProvider<OrganizationDataOwnershipPolicyRequirementFactory> sutProvider)
    {
        // Arrange
        var requirement = sutProvider.Sut.Create([]);
        var organizationId = Guid.NewGuid();

        // Act
        var result = requirement.GetDefaultCollectionRequest(organizationId);

        // Assert
        Assert.Equal(Guid.Empty, result.OrganizationUserId);
        Assert.False(result.ShouldCreateDefaultCollection);
    }

    [Theory, BitAutoData]
    public void GetDefaultCollectionRequest_WithMixedStatuses(
        [PolicyDetails(PolicyType.OrganizationDataOwnership)] PolicyDetails[] policies,
        SutProvider<OrganizationDataOwnershipPolicyRequirementFactory> sutProvider)
    {
        // Arrange
        var requirement = sutProvider.Sut.Create(policies);

        var confirmedPolicy = policies[0];
        var acceptedPolicy = policies[1];

        confirmedPolicy.OrganizationUserStatus = OrganizationUserStatusType.Confirmed;
        acceptedPolicy.OrganizationUserStatus = OrganizationUserStatusType.Accepted;

        // Act
        var confirmedResult = requirement.GetDefaultCollectionRequest(confirmedPolicy.OrganizationId);
        var acceptedResult = requirement.GetDefaultCollectionRequest(acceptedPolicy.OrganizationId);

        // Assert
        Assert.Equal(Guid.Empty, acceptedResult.OrganizationUserId);
        Assert.False(acceptedResult.ShouldCreateDefaultCollection);

        Assert.Equal(confirmedPolicy.OrganizationUserId, confirmedResult.OrganizationUserId);
        Assert.True(confirmedResult.ShouldCreateDefaultCollection);
    }
}
