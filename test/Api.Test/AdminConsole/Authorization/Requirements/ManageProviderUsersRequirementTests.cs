using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Core.AdminConsole.Context;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization.Requirements;

[SutProviderCustomize]
public class ManageProviderUsersRequirementTests
{
    [Theory, BitAutoData]
    public async Task AuthorizeAsync_WhenUserIsProviderAdmin_ThenRequestShouldBeAuthorized(
        SutProvider<ManageProviderUsersRequirement> sutProvider)
    {
        var providerClaims = new CurrentContextProvider { Id = Guid.NewGuid(), Type = ProviderUserType.ProviderAdmin };

        var actual = await sutProvider.Sut.AuthorizeAsync(providerClaims);

        Assert.True(actual);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeAsync_WhenUserIsServiceUser_ThenRequestShouldBeDenied(
        SutProvider<ManageProviderUsersRequirement> sutProvider)
    {
        var providerClaims = new CurrentContextProvider { Id = Guid.NewGuid(), Type = ProviderUserType.ServiceUser };

        var actual = await sutProvider.Sut.AuthorizeAsync(providerClaims);

        Assert.False(actual);
    }

    [Theory, BitAutoData]
    public async Task AuthorizeAsync_WhenUserIsNotProviderMember_ThenRequestShouldBeDenied(
        SutProvider<ManageProviderUsersRequirement> sutProvider)
    {
        var actual = await sutProvider.Sut.AuthorizeAsync(null);

        Assert.False(actual);
    }
}
