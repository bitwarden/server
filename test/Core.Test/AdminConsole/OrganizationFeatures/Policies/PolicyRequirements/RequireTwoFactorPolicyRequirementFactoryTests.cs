using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
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
    public void RequireTwoFactor_WithNoPolicies_ReturnsFalse(SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.RequireTwoFactor);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void RequireTwoFactor_WithNonExemptStatus_ReturnsTrue(
        OrganizationUserStatusType userStatus,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = userStatus
            }
        ]);

        Assert.True(actual.RequireTwoFactor);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    public void RequireTwoFactor_WithExemptStatus_ReturnsFalse(
        OrganizationUserStatusType userStatus,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = userStatus
            }
        ]);

        Assert.False(actual.RequireTwoFactor);
    }
}
