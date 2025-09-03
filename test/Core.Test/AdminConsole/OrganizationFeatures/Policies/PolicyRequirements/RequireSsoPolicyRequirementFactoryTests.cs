using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

[SutProviderCustomize]
public class RequireSsoPolicyRequirementFactoryTests
{
    [Theory, BitAutoData]
    public void CanUsePasskeyLogin_WithNoPolicies_ReturnsTrue(
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.True(actual.CanUsePasskeyLogin);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void CanUsePasskeyLogin_WithoutExemptStatus_ReturnsFalse(
        OrganizationUserStatusType userStatus,
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                PolicyType = PolicyType.RequireSso,
                OrganizationUserStatus = userStatus
            }
        ]);

        Assert.False(actual.CanUsePasskeyLogin);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    public void CanUsePasskeyLogin_WithExemptStatus_ReturnsTrue(
        OrganizationUserStatusType userStatus,
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                PolicyType = PolicyType.RequireSso,
                OrganizationUserStatus = userStatus
            }
        ]);

        Assert.True(actual.CanUsePasskeyLogin);
    }

    [Theory, BitAutoData]
    public void SsoRequired_WithNoPolicies_ReturnsFalse(
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.SsoRequired);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    public void SsoRequired_WithoutExemptStatus_ReturnsFalse(
        OrganizationUserStatusType userStatus,
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                PolicyType = PolicyType.RequireSso,
                OrganizationUserStatus = userStatus
            }
        ]);

        Assert.False(actual.SsoRequired);
    }

    [Theory, BitAutoData]
    public void SsoRequired_WithExemptStatus_ReturnsTrue(
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                PolicyType = PolicyType.RequireSso,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed
            }
        ]);

        Assert.True(actual.SsoRequired);
    }
}
