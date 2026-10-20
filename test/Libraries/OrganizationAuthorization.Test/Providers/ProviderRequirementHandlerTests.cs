using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Providers;
using Bit.Core.AdminConsole.Context;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization.Providers;

[SutProviderCustomize]
public class ProviderRequirementHandlerTests
{
    [Theory]
    [BitAutoData((string)null)]
    [BitAutoData("malformed guid")]
    public async Task IfInvalidProviderId_Throws(string providerId, Guid userId, SutProvider<ProviderRequirementHandler> sutProvider)
    {
        // Arrange
        ArrangeRouteAndUser(sutProvider, providerId, userId);
        var testRequirement = Substitute.For<IProviderRequirement>();
        var authContext = new AuthorizationHandlerContext([testRequirement], new ClaimsPrincipal(), null);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.HandleAsync(authContext));
        Assert.Contains(HttpContextExtensions.NoProviderIdError, exception.Message);
        Assert.False(authContext.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task IfHttpContextIsNull_Throws(SutProvider<ProviderRequirementHandler> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext = null;
        var testRequirement = Substitute.For<IProviderRequirement>();
        var authContext = new AuthorizationHandlerContext([testRequirement], new ClaimsPrincipal(), null);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.HandleAsync(authContext));
        Assert.Contains(ProviderRequirementHandler.NoHttpContextError, exception.Message);
        Assert.False(authContext.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task IfUserIdIsNull_DoesNotAuthorize(Guid providerId, SutProvider<ProviderRequirementHandler> sutProvider)
    {
        // Arrange
        ArrangeRouteAndUser(sutProvider, providerId.ToString(), null);
        var testRequirement = Substitute.For<IProviderRequirement>();
        var authContext = new AuthorizationHandlerContext([testRequirement], new ClaimsPrincipal(), null);

        // Act
        await sutProvider.Sut.HandleAsync(authContext);

        // Assert — requirement is not invoked and context has not succeeded
        testRequirement.DidNotReceive().Authorize(Arg.Any<CurrentContextProvider?>());
        Assert.False(authContext.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task DoesNotAuthorize_IfAuthorizeAsync_ReturnsFalse(
        SutProvider<ProviderRequirementHandler> sutProvider, Guid providerId, Guid userId)
    {
        // Arrange
        ArrangeRouteAndUser(sutProvider, providerId.ToString(), userId);

        var testRequirement = Substitute.For<IProviderRequirement>();
        testRequirement.Authorize(null).ReturnsForAnyArgs(false);
        var authContext = new AuthorizationHandlerContext([testRequirement], new ClaimsPrincipal(), null);

        // Act
        await sutProvider.Sut.HandleAsync(authContext);

        // Assert
        testRequirement.Received(1).Authorize(null);
        Assert.False(authContext.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task Authorizes_IfAuthorizeAsync_ReturnsTrue(
        SutProvider<ProviderRequirementHandler> sutProvider, Guid providerId, Guid userId)
    {
        // Arrange
        ArrangeRouteAndUser(sutProvider, providerId.ToString(), userId);

        var testRequirement = Substitute.For<IProviderRequirement>();
        testRequirement.Authorize(null).ReturnsForAnyArgs(true);
        var authContext = new AuthorizationHandlerContext([testRequirement], new ClaimsPrincipal(), null);

        // Act
        await sutProvider.Sut.HandleAsync(authContext);

        // Assert
        testRequirement.Received(1).Authorize(null);
        Assert.True(authContext.HasSucceeded);
    }

    private static void ArrangeRouteAndUser(SutProvider<ProviderRequirementHandler> sutProvider, string providerIdRouteValue,
        Guid? userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["providerId"] = providerIdRouteValue;
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext = httpContext;
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
    }
}
