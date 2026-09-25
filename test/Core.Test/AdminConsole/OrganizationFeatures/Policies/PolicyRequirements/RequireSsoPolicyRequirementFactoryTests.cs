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
    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    [BitAutoData(OrganizationUserStatusType.Staged)]
    public void Enforce_UserWithExemptStatus_ReturnsFalse(
        OrganizationUserStatusType userStatus,
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var enforce = sutProvider.Sut.Enforce(new PolicyDetails
        {
            PolicyType = PolicyType.RequireSso,
            OrganizationUserType = OrganizationUserType.User,
            OrganizationUserStatus = userStatus
        });

        Assert.False(enforce);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void Enforce_MemberUser_ReturnsTrue(
        OrganizationUserStatusType userStatus,
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var enforce = sutProvider.Sut.Enforce(new PolicyDetails
        {
            PolicyType = PolicyType.RequireSso,
            OrganizationUserType = OrganizationUserType.User,
            OrganizationUserStatus = userStatus
        });

        Assert.True(enforce);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public void Enforce_OwnerOrAdmin_ReturnsFalse(
        OrganizationUserType userType,
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var enforce = sutProvider.Sut.Enforce(new PolicyDetails
        {
            PolicyType = PolicyType.RequireSso,
            OrganizationUserType = userType,
            OrganizationUserStatus = OrganizationUserStatusType.Confirmed
        });

        Assert.False(enforce);
    }

    [Theory, BitAutoData]
    public void CanUsePasskeyLogin_WithNoEnforcedPolicies_ReturnsTrue(
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.True(actual.CanUsePasskeyLogin);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void CanUsePasskeyLogin_WithEnforcedPolicy_ReturnsFalse(
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

    [Theory, BitAutoData]
    public void SsoRequired_WithNoEnforcedPolicies_ReturnsFalse(
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.SsoRequired);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void SsoRequired_WithEnforcedPolicy_ReturnsTrue(
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

        Assert.True(actual.SsoRequired);
    }

    [Theory, BitAutoData]
    public void OrganizationIds_WithNoEnforcedPolicies_ReturnsEmpty(
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.Empty(actual.OrganizationIds);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void OrganizationIds_SingleEnforcedPolicy_ReturnsThatOrgId(
        OrganizationUserStatusType userStatus,
        Guid organizationId,
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                OrganizationId = organizationId,
                PolicyType = PolicyType.RequireSso,
                OrganizationUserStatus = userStatus
            }
        ]);

        Assert.Equal(new[] { organizationId }, actual.OrganizationIds);
    }

    [Theory, BitAutoData]
    public void OrganizationIds_MultipleEnforcedPolicies_ReturnsAllOrgIds(
        Guid organizationIdA,
        Guid organizationIdB,
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                OrganizationId = organizationIdA,
                PolicyType = PolicyType.RequireSso,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed
            },
            new PolicyDetails
            {
                OrganizationId = organizationIdB,
                PolicyType = PolicyType.RequireSso,
                OrganizationUserStatus = OrganizationUserStatusType.Accepted
            }
        ]);

        Assert.Equal(2, actual.OrganizationIds.Count);
        Assert.Contains(organizationIdA, actual.OrganizationIds);
        Assert.Contains(organizationIdB, actual.OrganizationIds);
    }
}
