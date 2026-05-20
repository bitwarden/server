using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
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

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void SsoRequired_PoliciesInAcceptedStateEnabled_AcceptedOrConfirmed_ReturnsTrue(
        OrganizationUserStatusType userStatus,
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PoliciesInAcceptedState)
            .Returns(true);

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
    public void SsoRequired_PoliciesInAcceptedStateDisabled_Accepted_ReturnsFalse(
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PoliciesInAcceptedState)
            .Returns(false);

        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                PolicyType = PolicyType.RequireSso,
                OrganizationUserStatus = OrganizationUserStatusType.Accepted
            }
        ]);

        Assert.False(actual.SsoRequired);
    }

    [Theory, BitAutoData]
    public void SsoRequired_PoliciesInAcceptedStateDisabled_Confirmed_ReturnsTrue(
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PoliciesInAcceptedState)
            .Returns(false);

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

    [Theory, BitAutoData]
    public void SsoOrganizationIds_WithNoPolicies_ReturnsEmpty(
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.Empty(actual.SsoOrganizationIds);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void SsoOrganizationIds_SinglePolicy_ReturnsThatOrgId(
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

        Assert.Equal(new[] { organizationId }, actual.SsoOrganizationIds);
    }

    [Theory, BitAutoData]
    public void SsoOrganizationIds_MultiplePolicies_ReturnsAllOrgIds(
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

        Assert.Equal(2, actual.SsoOrganizationIds.Count);
        Assert.Contains(organizationIdA, actual.SsoOrganizationIds);
        Assert.Contains(organizationIdB, actual.SsoOrganizationIds);
    }

    [Theory, BitAutoData]
    public void SsoOrganizationIds_AcceptedStatusWithFeatureFlagOff_IncludesOrgIdEvenWhenSsoNotYetRequired(
        Guid organizationId,
        SutProvider<RequireSsoPolicyRequirementFactory> sutProvider)
    {
        // SsoOrganizationIds reflects "policy applies to this membership" — independent of whether
        // SsoRequired is currently true for this user. Callers gate on SsoRequired before reading
        // the org ids.
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PoliciesInAcceptedState)
            .Returns(false);

        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                OrganizationId = organizationId,
                PolicyType = PolicyType.RequireSso,
                OrganizationUserStatus = OrganizationUserStatusType.Accepted
            }
        ]);

        Assert.False(actual.SsoRequired);
        Assert.Equal(new[] { organizationId }, actual.SsoOrganizationIds);
    }
}
