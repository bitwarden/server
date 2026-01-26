using Bit.Api.AdminConsole.Models.Response.Helpers;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Response.Helpers;

public class PolicyStatusResponsesTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GetSingleOrgPolicyDetailResponseAsync_WhenIsSingleOrgTypeAndHasVerifiedDomains_ShouldReturnExpectedToggleState(
        bool policyEnabled,
        bool expectedCanToggle)
    {
        var policy = new PolicyStatus(Guid.NewGuid(), PolicyType.SingleOrg) { Enabled = policyEnabled };

        var querySub = Substitute.For<IOrganizationHasVerifiedDomainsQuery>();
        querySub.HasVerifiedDomainsAsync(policy.OrganizationId)
            .Returns(true);

        var result = await policy.GetSingleOrgPolicyStatusResponseAsync(querySub);

        Assert.Equal(expectedCanToggle, result.CanToggleState);
    }

    [Fact]
    public async Task GetSingleOrgPolicyDetailResponseAsync_WhenIsNotSingleOrgType_ThenShouldThrowArgumentException()
    {
        var policy = new PolicyStatus(Guid.NewGuid(), PolicyType.TwoFactorAuthentication);

        var querySub = Substitute.For<IOrganizationHasVerifiedDomainsQuery>();
        querySub.HasVerifiedDomainsAsync(policy.OrganizationId)
            .Returns(true);

        var action = async () => await policy.GetSingleOrgPolicyStatusResponseAsync(querySub);

        await Assert.ThrowsAsync<ArgumentException>("policy", action);
    }

    [Fact]
    public async Task GetSingleOrgPolicyDetailResponseAsync_WhenIsSingleOrgTypeAndDoesNotHaveVerifiedDomains_ThenShouldBeAbleToToggle()
    {
        var policy = new PolicyStatus(Guid.NewGuid(), PolicyType.SingleOrg);

        var querySub = Substitute.For<IOrganizationHasVerifiedDomainsQuery>();
        querySub.HasVerifiedDomainsAsync(policy.OrganizationId)
            .Returns(false);

        var result = await policy.GetSingleOrgPolicyStatusResponseAsync(querySub);

        Assert.True(result.CanToggleState);
    }
}
