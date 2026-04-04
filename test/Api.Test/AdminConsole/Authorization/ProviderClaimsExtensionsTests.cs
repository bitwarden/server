using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Auth.Identity;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

public class ProviderClaimsExtensionsTests
{
    [Theory, BitAutoData]
    public void GetCurrentContextProvider_WhenUserIsProviderAdmin_ReturnsProviderAdminClaims(Guid providerId)
    {
        var claims = new[] { new Claim(Claims.ProviderAdmin, providerId.ToString()) };
        var claimsPrincipal = MakeClaimsPrincipal(claims);

        var result = claimsPrincipal.GetCurrentContextProvider(providerId);

        Assert.NotNull(result);
        Assert.Equal(providerId, result.Id);
        Assert.Equal(ProviderUserType.ProviderAdmin, result.Type);
    }

    [Theory, BitAutoData]
    public void GetCurrentContextProvider_WhenUserIsServiceUser_ReturnsServiceUserClaims(Guid providerId)
    {
        var claims = new[] { new Claim(Claims.ProviderServiceUser, providerId.ToString()) };
        var claimsPrincipal = MakeClaimsPrincipal(claims);

        var result = claimsPrincipal.GetCurrentContextProvider(providerId);

        Assert.NotNull(result);
        Assert.Equal(providerId, result.Id);
        Assert.Equal(ProviderUserType.ServiceUser, result.Type);
    }

    [Theory, BitAutoData]
    public void GetCurrentContextProvider_WhenUserIsNotProviderMember_ReturnsNull(Guid providerId)
    {
        var claimsPrincipal = MakeClaimsPrincipal([]);

        var result = claimsPrincipal.GetCurrentContextProvider(providerId);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public void GetCurrentContextProvider_WhenClaimsContainDifferentProviderId_ReturnsNull(Guid providerId, Guid otherProviderId)
    {
        var claims = new[] { new Claim(Claims.ProviderAdmin, otherProviderId.ToString()) };
        var claimsPrincipal = MakeClaimsPrincipal(claims);

        var result = claimsPrincipal.GetCurrentContextProvider(providerId);

        Assert.Null(result);
    }

    private static ClaimsPrincipal MakeClaimsPrincipal(IEnumerable<Claim> claims)
    {
        var principal = new ClaimsPrincipal();
        principal.AddIdentities([new ClaimsIdentity(claims)]);
        return principal;
    }
}
