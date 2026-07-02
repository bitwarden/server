using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Services.Implementations;
using LaunchDarkly.Sdk;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class ServerContextBuilderTests
{
    private const string AnonymousUser = "25a15cac-58cf-4ac0-ad0f-b17c4bd92294";

    [Fact]
    public void Build_NoHttpContext_ReturnsAnonymousDefaultContext()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var builder = new ServerContextBuilder(accessor);

        var context = builder.Build();

        Assert.False(context.Multiple);
        Assert.Equal(ContextKind.Default, context.Kind);
        Assert.Equal(AnonymousUser, context.Key);
        Assert.True(context.Anonymous);
    }

    [Fact]
    public void Build_AuthenticatedUserWithDevice_ReturnsMultiKindContext()
    {
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.IdentityClientType.Returns(IdentityClientType.User);
        currentContext.UserId.Returns(userId);
        currentContext.DeviceIdentifier.Returns("device-key");
        currentContext.DeviceType.Returns(DeviceType.ChromeBrowser);

        var builder = new ServerContextBuilder(BuildAccessor(currentContext));

        var context = builder.Build();

        Assert.True(context.Multiple);

        Assert.True(context.TryGetContextByKind(ContextKind.Default, out var user));
        Assert.Equal(userId.ToString(), user.Key);
        Assert.False(user.Anonymous);

        Assert.True(context.TryGetContextByKind(ContextKind.Of("device"), out var device));
        Assert.Equal("device-key", device.Key);
    }

    [Fact]
    public void Build_UnauthenticatedUser_ReturnsAnonymousUserContext()
    {
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.IdentityClientType.Returns(IdentityClientType.User);
        currentContext.UserId.Returns((Guid?)null);

        var builder = new ServerContextBuilder(BuildAccessor(currentContext));

        var context = builder.Build();

        Assert.True(context.TryGetContextByKind(ContextKind.Default, out var user));
        Assert.Equal(AnonymousUser, user.Key);
        Assert.True(user.Anonymous);
    }

    [Fact]
    public void Build_OrganizationClientType_ReturnsOrganizationContext()
    {
        var orgId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var currentContext = Substitute.For<ICurrentContext>();
        currentContext.IdentityClientType.Returns(IdentityClientType.Organization);
        currentContext.OrganizationId.Returns(orgId);

        var builder = new ServerContextBuilder(BuildAccessor(currentContext));

        var context = builder.Build();

        Assert.True(context.TryGetContextByKind(ContextKind.Of("organization"), out var org));
        Assert.Equal(orgId.ToString(), org.Key);
    }

    private static IHttpContextAccessor BuildAccessor(ICurrentContext currentContext)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => currentContext);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider().CreateScope().ServiceProvider,
        };

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return accessor;
    }
}
