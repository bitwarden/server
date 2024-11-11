using AutoFixture;
using Bit.Api.AdminConsole.Models.Response.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Response.Helpers;

public class PolicyDetailResponsesTests
{
    [Fact]
    public async Task GetSingleOrgPolicyDetailResponseAsync_GivenPolicyEntity_WhenIsSingleOrgTypeAndHasVerifiedDomains_ThenShouldNotBeAbleToToggle()
    {
        var fixture = new Fixture();

        var policy = fixture.Build<Policy>()
            .Without(p => p.Data)
            .With(p => p.Type, PolicyType.SingleOrg)
            .Create();

        var querySub = Substitute.For<IOrganizationHasVerifiedDomainsQuery>();
        querySub.HasVerifiedDomainsAsync(policy.OrganizationId)
            .Returns(true);

        var result = await policy.GetSingleOrgPolicyDetailResponseAsync(querySub);

        Assert.False(result.CanToggleState);
    }

    [Fact]
    public async Task GetSingleOrgPolicyDetailResponseAsync_GivenPolicyEntity_WhenIsNotSingleOrgType_ThenShouldThrowArgumentException()
    {
        var fixture = new Fixture();

        var policy = fixture.Build<Policy>()
            .Without(p => p.Data)
            .With(p => p.Type, PolicyType.TwoFactorAuthentication)
            .Create();

        var querySub = Substitute.For<IOrganizationHasVerifiedDomainsQuery>();
        querySub.HasVerifiedDomainsAsync(policy.OrganizationId)
            .Returns(true);

        var action = async () => await policy.GetSingleOrgPolicyDetailResponseAsync(querySub);

        await Assert.ThrowsAsync<ArgumentException>("policy", action);
    }

    [Fact]
    public async Task GetSingleOrgPolicyDetailResponseAsync_GivenPolicyEntity_WhenIsSingleOrgTypeAndDoesNotHaveVerifiedDomains_ThenShouldBeAbleToToggle()
    {
        var fixture = new Fixture();

        var policy = fixture.Build<Policy>()
            .Without(p => p.Data)
            .With(p => p.Type, PolicyType.SingleOrg)
            .Create();

        var querySub = Substitute.For<IOrganizationHasVerifiedDomainsQuery>();
        querySub.HasVerifiedDomainsAsync(policy.OrganizationId)
            .Returns(false);

        var result = await policy.GetSingleOrgPolicyDetailResponseAsync(querySub);

        Assert.True(result.CanToggleState);
    }
}
