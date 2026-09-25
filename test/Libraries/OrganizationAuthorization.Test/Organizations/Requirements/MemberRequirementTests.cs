using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization.Requirements;

[SutProviderCustomize]
public class MemberRequirementTests
{
    [Theory]
    [CurrentContextOrganizationCustomize]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task AuthorizeAsync_WhenUserIsOrganizationMember_ThenRequestShouldBeAuthorized(
        OrganizationUserType type,
        CurrentContextOrganization organization,
        SutProvider<MemberRequirement> sutProvider)
    {
        organization.Type = type;

        var actual = await sutProvider.Sut.AuthorizeAsync(organization, () => Task.FromResult(false));

        Assert.True(actual);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeAsync_WhenUserIsNotOrganizationMember_ThenRequestShouldBeDenied(
        SutProvider<MemberRequirement> sutProvider)
    {
        var actual = await sutProvider.Sut.AuthorizeAsync(null, () => Task.FromResult(false));

        Assert.False(actual);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeAsync_WhenUserIsProviderButNotMember_ThenRequestShouldBeDenied(
        SutProvider<MemberRequirement> sutProvider)
    {
        var actual = await sutProvider.Sut.AuthorizeAsync(null, () => Task.FromResult(true));

        Assert.False(actual);
    }
}
