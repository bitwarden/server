using System.Security.Claims;
using Bit.Core.AdminConsole.Context;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Context;

public class ScopedOrganizationApiKeyContextTests
{
    [Theory]
    [InlineData("organization.a9a4ad6f-5c1e-4f4a-9a0b-3f2d6c1e8b70.0b7f3c2e-9d41-4e6a-8f15-2c7a9e4d1b36", "api.organization.members.write", true)]
    [InlineData("organization.a9a4ad6f-5c1e-4f4a-9a0b-3f2d6c1e8b70", "api.organization", false)]
    [InlineData("organization.a9a4ad6f-5c1e-4f4a-9a0b-3f2d6c1e8b70", "api.scim", false)]
    [InlineData("organization.a9a4ad6f-5c1e-4f4a-9a0b-3f2d6c1e8b70", null, false)]
    [InlineData("organization.a9a4ad6f-5c1e-4f4a-9a0b-3f2d6c1e8b70.12345", "api.organization.members.write", false)]
    [InlineData("organization.12345.0b7f3c2e-9d41-4e6a-8f15-2c7a9e4d1b36", "api.organization.members.write", false)]
    [InlineData("organization.a9a4ad6f-5c1e-4f4a-9a0b-3f2d6c1e8b70.0b7f3c2e-9d41-4e6a-8f15-2c7a9e4d1b36.extra", "api.organization.members.write", false)]
    [InlineData("web", "api", false)]
    public void IsScopedOrganizationApiKey_ReflectsClientIdShape(string clientId, string? scope, bool expected)
    {
        var claims = new List<Claim>
        {
            new("client_id", clientId),
            new("client_sub", Guid.NewGuid().ToString())
        };
        if (scope != null)
        {
            claims.Add(new Claim("scope", scope));
        }

        var sut = CreateSut(new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) });

        Assert.Equal(expected, sut.IsScopedOrganizationApiKey);
    }

    [Fact]
    public void IsScopedOrganizationApiKey_UserToken_ReturnsFalse()
    {
        var claims = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString()),
            new("client_id", "web"),
            new("scope", "api")
        };

        var sut = CreateSut(new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) });

        Assert.False(sut.IsScopedOrganizationApiKey);
    }

    [Fact]
    public void IsScopedOrganizationApiKey_NoClientIdClaim_ReturnsFalse()
    {
        var sut = CreateSut(new DefaultHttpContext());

        Assert.False(sut.IsScopedOrganizationApiKey);
    }

    [Fact]
    public void IsScopedOrganizationApiKey_NoHttpContext_ReturnsFalse()
    {
        var sut = CreateSut(null);

        Assert.False(sut.IsScopedOrganizationApiKey);
    }

    private static ScopedOrganizationApiKeyContext CreateSut(HttpContext? httpContext)
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);
        return new ScopedOrganizationApiKeyContext(httpContextAccessor);
    }
}
