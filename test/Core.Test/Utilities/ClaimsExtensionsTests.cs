using System.Security.Claims;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class ClaimsExtensionsTests
{
    [Fact]
    public void HasSSOIdP_Returns_True_When_The_Claims_Has_One_Of_Type_IdP_And_Value_Sso()
    {
        var claims = new List<Claim> { new Claim("idp", "sso") };
        Assert.True(claims.HasSsoIdP());
    }

    [Fact]
    public void HasSSOIdP_Returns_False_When_The_Claims_Has_One_Of_Type_IdP_And_Value_Is_Not_Sso()
    {
        var claims = new List<Claim> { new Claim("idp", "asdfasfd") };
        Assert.False(claims.HasSsoIdP());
    }

    [Fact]
    public void HasSSOIdP_Returns_False_When_The_Claims_Has_No_One_Of_Type_IdP()
    {
        var claims = new List<Claim> { new Claim("qweqweq", "sso") };
        Assert.False(claims.HasSsoIdP());
    }

    [Fact]
    public void HasSSOIdP_Returns_False_When_The_Claims_Are_Empty()
    {
        var claims = new List<Claim>();
        Assert.False(claims.HasSsoIdP());
    }
}
