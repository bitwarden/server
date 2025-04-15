using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

[SutProviderCustomize]
public class RequireSsoPolicyRequirementFactoryTests
{
    [Theory, BitAutoData]
    public void CanUsePasskeyLogin_WithNoPolicies_ReturnsFalse(SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.CanUsePasskeyLogin);
    }

    [Theory, BitAutoData]
    public void CanUsePasskeyLogin_WithInvalidPolicies_ReturnsFalse(
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails { PolicyType = PolicyType.RequireSso, OrganizationUserStatus = OrganizationUserStatusType.Revoked },
            new PolicyDetails { PolicyType = PolicyType.RequireSso, OrganizationUserStatus = OrganizationUserStatusType.Invited }
        ]);

        Assert.False(actual.CanUsePasskeyLogin);
    }

    [Theory, BitAutoData]
    public void CanUsePasskeyLogin_WithValidPolicies_ReturnsTrue(
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails { PolicyType = PolicyType.RequireSso, OrganizationUserStatus = OrganizationUserStatusType.Accepted },
            new PolicyDetails { PolicyType = PolicyType.RequireSso, OrganizationUserStatus = OrganizationUserStatusType.Confirmed }
        ]);

        Assert.True(actual.CanUsePasskeyLogin);
    }

    [Theory, BitAutoData]
    public void SsoRequired_WithNoPolicies_ReturnsFalse(SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.SsoRequired);
    }

    [Theory, BitAutoData]
    public void SsoRequired_WithInvalidPolicies_ReturnsFalse(
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails { PolicyType = PolicyType.RequireSso, OrganizationUserStatus = OrganizationUserStatusType.Revoked },
            new PolicyDetails { PolicyType = PolicyType.RequireSso, OrganizationUserStatus = OrganizationUserStatusType.Invited },
            new PolicyDetails { PolicyType = PolicyType.RequireSso, OrganizationUserStatus = OrganizationUserStatusType.Accepted }
        ]);

        Assert.False(actual.SsoRequired);
    }

    [Theory, BitAutoData]
    public void SsoRequired_WithValidPolicies_ReturnsTrue(
        [PolicyDetails(PolicyType.RequireSso, userStatus: OrganizationUserStatusType.Confirmed)] PolicyDetails[] policies,
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.SsoRequired);
    }
}
