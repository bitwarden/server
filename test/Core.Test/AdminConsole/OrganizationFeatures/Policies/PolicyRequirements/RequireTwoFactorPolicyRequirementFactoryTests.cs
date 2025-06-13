using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

[SutProviderCustomize]
public class RequireTwoFactorPolicyRequirementFactoryTests
{
    [Theory]
    [BitAutoData]
    public void IsTwoFactorRequiredForOrganization_WithNoPolicies_ReturnsFalse(
        Guid organizationId,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.IsTwoFactorRequiredForOrganization(organizationId));
    }

    [Theory]
    [BitAutoData]
    public void IsTwoFactorRequiredForOrganization_WithOrganizationPolicy_ReturnsTrue(
        Guid organizationId,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                OrganizationId = organizationId,
                PolicyType = PolicyType.TwoFactorAuthentication,
            }
        ]);

        Assert.True(actual.IsTwoFactorRequiredForOrganization(organizationId));
    }

    [Theory]
    [BitAutoData]
    public void IsTwoFactorRequiredForOrganization_WithOtherOrganizationPolicy_ReturnsFalse(
        Guid organizationId,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                OrganizationId = Guid.NewGuid(),
                PolicyType = PolicyType.TwoFactorAuthentication,
            },
        ]);

        Assert.False(actual.IsTwoFactorRequiredForOrganization(organizationId));
    }

    [Theory, BitAutoData]
    public void OrganizationsRequiringTwoFactor_WithNoPolicies_ReturnsEmptyCollection(
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.Empty(actual.OrganizationsRequiringTwoFactor);
    }

    [Theory, BitAutoData]
    public void OrganizationsRequiringTwoFactor_WithMultiplePolicies_ReturnsActiveMemberships(
        Guid orgId1, Guid orgUserId1, Guid orgId2, Guid orgUserId2,
        Guid orgId3, Guid orgUserId3, Guid orgId4, Guid orgUserId4,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var policies = new[]
        {
            new PolicyDetails
            {
                OrganizationId = orgId1,
                OrganizationUserId = orgUserId1,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = OrganizationUserStatusType.Accepted
            },
            new PolicyDetails
            {
                OrganizationId = orgId2,
                OrganizationUserId = orgUserId2,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed
            },
            new PolicyDetails
            {
                OrganizationId = orgId3,
                OrganizationUserId = orgUserId3,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = OrganizationUserStatusType.Invited
            },
            new PolicyDetails
            {
                OrganizationId = orgId4,
                OrganizationUserId = orgUserId4,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = OrganizationUserStatusType.Revoked
            }
        };

        var actual = sutProvider.Sut.Create(policies);

        var result = actual.OrganizationsRequiringTwoFactor.ToList();
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.OrganizationId == orgId1 && p.OrganizationUserId == orgUserId1);
        Assert.Contains(result, p => p.OrganizationId == orgId2 && p.OrganizationUserId == orgUserId2);
        Assert.DoesNotContain(result, p => p.OrganizationId == orgId3 && p.OrganizationUserId == orgUserId3);
        Assert.DoesNotContain(result, p => p.OrganizationId == orgId4 && p.OrganizationUserId == orgUserId4);
    }
}
