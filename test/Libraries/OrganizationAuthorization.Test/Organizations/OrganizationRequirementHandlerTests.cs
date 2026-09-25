using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization;

[SutProviderCustomize]
public class OrganizationRequirementHandlerTests
{
    [Theory]
    [BitAutoData((string)null)]
    [BitAutoData("malformed guid")]
    public async Task IfInvalidOrganizationId_Throws(string orgId, Guid userId, SutProvider<OrganizationRequirementHandler> sutProvider)
    {
        // Arrange
        ArrangeRouteAndUser(sutProvider, orgId, userId);
        var testRequirement = Substitute.For<IOrganizationRequirement>();
        var authContext = new AuthorizationHandlerContext([testRequirement], new ClaimsPrincipal(), null);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.HandleAsync(authContext));
        Assert.Contains(HttpContextExtensions.NoOrgIdError, exception.Message);
        Assert.False(authContext.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task IfHttpContextIsNull_Throws(SutProvider<OrganizationRequirementHandler> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext = null;
        var testRequirement = Substitute.For<IOrganizationRequirement>();
        var authContext = new AuthorizationHandlerContext([testRequirement], new ClaimsPrincipal(), null);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.HandleAsync(authContext));
        Assert.Contains(OrganizationRequirementHandler.NoHttpContextError, exception.Message);
        Assert.False(authContext.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task IfUserIdIsNull_Throws(Guid orgId, SutProvider<OrganizationRequirementHandler> sutProvider)
    {
        // Arrange
        ArrangeRouteAndUser(sutProvider, orgId.ToString(), null);
        var testRequirement = Substitute.For<IOrganizationRequirement>();
        var authContext = new AuthorizationHandlerContext([testRequirement], new ClaimsPrincipal(), null);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sutProvider.Sut.HandleAsync(authContext));
        Assert.Contains(OrganizationRequirementHandler.NoUserIdError, exception.Message);
        Assert.False(authContext.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task DoesNotAuthorize_IfAuthorizeAsync_ReturnsFalse(
        SutProvider<OrganizationRequirementHandler> sutProvider, Guid organizationId, Guid userId)
    {
        // Arrange route values
        ArrangeRouteAndUser(sutProvider, organizationId.ToString(), userId);

        // Arrange requirement
        var testRequirement = Substitute.For<IOrganizationRequirement>();
        testRequirement
            .AuthorizeAsync(null, Arg.Any<Func<Task<bool>>>())
            .ReturnsForAnyArgs(false);
        var authContext = new AuthorizationHandlerContext([testRequirement], new ClaimsPrincipal(), null);

        // Act
        await sutProvider.Sut.HandleAsync(authContext);

        // Assert
        await testRequirement.Received(1).AuthorizeAsync(null, Arg.Any<Func<Task<bool>>>());
        Assert.False(authContext.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task Authorizes_IfAuthorizeAsync_ReturnsTrue(
        SutProvider<OrganizationRequirementHandler> sutProvider, Guid organizationId, Guid userId)
    {
        // Arrange route values
        ArrangeRouteAndUser(sutProvider, organizationId.ToString(), userId);

        // Arrange requirement
        var testRequirement = Substitute.For<IOrganizationRequirement>();
        testRequirement
            .AuthorizeAsync(null, Arg.Any<Func<Task<bool>>>())
            .ReturnsForAnyArgs(true);
        var authContext = new AuthorizationHandlerContext([testRequirement], new ClaimsPrincipal(), null);

        // Act
        await sutProvider.Sut.HandleAsync(authContext);

        // Assert
        await testRequirement.Received(1).AuthorizeAsync(null, Arg.Any<Func<Task<bool>>>());
        Assert.True(authContext.HasSucceeded);
    }

    private static void ArrangeRouteAndUser(SutProvider<OrganizationRequirementHandler> sutProvider, string orgIdRouteValue,
        Guid? userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["orgId"] = orgIdRouteValue;
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext = httpContext;
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);
    }
}
