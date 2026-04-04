using Bit.Api.AdminConsole.Authorization.Providers.Requirements;
using Bit.Core.AdminConsole.Context;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization.Providers.Requirements;

[SutProviderCustomize]
public class ProviderAdminRequirementTests
{
    [Theory, BitAutoData]
    public void Authorize_WhenUserIsProviderAdmin_ThenRequestShouldBeAuthorized(
        SutProvider<ProviderAdminRequirement> sutProvider)
    {
        var providerClaims = new CurrentContextProvider { Id = Guid.NewGuid(), Type = ProviderUserType.ProviderAdmin };

        var actual = sutProvider.Sut.Authorize(providerClaims);

        Assert.True(actual);
    }

    [Theory, BitAutoData]
    public void Authorize_WhenUserIsServiceUser_ThenRequestShouldBeDenied(
        SutProvider<ProviderAdminRequirement> sutProvider)
    {
        var providerClaims = new CurrentContextProvider { Id = Guid.NewGuid(), Type = ProviderUserType.ServiceUser };

        var actual = sutProvider.Sut.Authorize(providerClaims);

        Assert.False(actual);
    }

    [Theory, BitAutoData]
    public void Authorize_WhenUserIsNotProviderMember_ThenRequestShouldBeDenied(
        SutProvider<ProviderAdminRequirement> sutProvider)
    {
        var actual = sutProvider.Sut.Authorize(null);

        Assert.False(actual);
    }
}
