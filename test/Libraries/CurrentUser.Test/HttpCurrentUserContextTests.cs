using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.CurrentUser.Test;

public class HttpCurrentUserContextTests
{
    [Fact]
    public void UserId_ReturnsGuid_WhenSubClaimIsValid()
    {
        var expected = Guid.NewGuid();
        var sut = Resolve(new Claim("sub", expected.ToString()));

        Assert.Equal(expected, sut.UserId);
    }

    [Fact]
    public void UserId_Throws_WhenSubClaimIsMissing()
    {
        var sut = Resolve();

        Assert.Throws<InvalidOperationException>(() => sut.UserId);
    }

    [Fact]
    public void UserId_Throws_WhenSubClaimIsNotAGuid()
    {
        var sut = Resolve(new Claim("sub", "not-a-guid"));

        Assert.Throws<InvalidOperationException>(() => sut.UserId);
    }

    [Fact]
    public void UserId_Throws_WhenNoHttpRequestIsInScope()
    {
        var sut = Resolve(new HttpContextAccessor());

        Assert.Throws<InvalidOperationException>(() => sut.UserId);
    }

    [Fact]
    public void UserId_IgnoresNameIdentifierClaim()
    {
        var sut = Resolve(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        Assert.Throws<InvalidOperationException>(() => sut.UserId);
    }

    [Fact]
    public void TryGetUserId_ReturnsTrueAndGuid_WhenSubClaimIsValid()
    {
        var expected = Guid.NewGuid();
        var sut = Resolve(new Claim("sub", expected.ToString()));

        Assert.True(sut.TryGetUserId(out var userId));
        Assert.Equal(expected, userId);
    }

    [Fact]
    public void TryGetUserId_ReturnsFalse_WhenSubClaimIsMissing()
    {
        var sut = Resolve();

        Assert.False(sut.TryGetUserId(out var userId));
        Assert.Equal(Guid.Empty, userId);
    }

    [Fact]
    public void TryGetUserId_ReturnsFalse_WhenSubClaimIsNotAGuid()
    {
        var sut = Resolve(new Claim("sub", "not-a-guid"));

        Assert.False(sut.TryGetUserId(out var userId));
        Assert.Equal(Guid.Empty, userId);
    }

    [Fact]
    public void TryGetUserId_ReturnsFalse_WhenNoHttpRequestIsInScope()
    {
        var sut = Resolve(new HttpContextAccessor());

        Assert.False(sut.TryGetUserId(out var userId));
        Assert.Equal(Guid.Empty, userId);
    }

    [Fact]
    public void TryGetUserId_IgnoresNameIdentifierClaim()
    {
        var sut = Resolve(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        Assert.False(sut.TryGetUserId(out _));
    }

    private static ICurrentUserContext Resolve(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims)),
        };
        return Resolve(new HttpContextAccessor { HttpContext = httpContext });
    }

    private static ICurrentUserContext Resolve(IHttpContextAccessor accessor)
    {
        var provider = new ServiceCollection()
            .AddSingleton(accessor)
            .AddCurrentUser()
            .BuildServiceProvider();

        return provider.CreateScope().ServiceProvider.GetRequiredService<ICurrentUserContext>();
    }
}
