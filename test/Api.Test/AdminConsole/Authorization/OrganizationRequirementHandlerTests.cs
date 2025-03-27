using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Context;
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
    [Theory, BitAutoData]
    public async Task IfNoOrganizationId_Throws(SutProvider<OrganizationRequirementHandler> sutProvider)
    {
        // Arrange
        ArrangeRouteValues(sutProvider, null); // no orgId in route
        var testRequirement = Substitute.For<IOrganizationRequirement>();
        var authContext = new AuthorizationHandlerContext([testRequirement], new ClaimsPrincipal(), null);

        // Act
        var exception = await Assert.ThrowsAsync<Exception>(() => sutProvider.Sut.HandleAsync(authContext));
        Assert.Contains("No organizationId found", exception.Message);
        Assert.False(authContext.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task IfInvalidOrganizationId_Throws(SutProvider<OrganizationRequirementHandler> sutProvider)
    {
        // Arrange
        ArrangeRouteValues(sutProvider, "malformed guid");
        var testRequirement = Substitute.For<IOrganizationRequirement>();
        var authContext = new AuthorizationHandlerContext([testRequirement], new ClaimsPrincipal(), null);

        // Act
        var exception = await Assert.ThrowsAsync<Exception>(() => sutProvider.Sut.HandleAsync(authContext));
        Assert.Contains("No organizationId found", exception.Message);
        Assert.False(authContext.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task DoesNotAuthorize_IfAuthorizeAsync_ReturnsFalse(SutProvider<OrganizationRequirementHandler> sutProvider, Guid organizationId)
    {
        // Arrange route values
        ArrangeRouteValues(sutProvider, organizationId.ToString());

        // Arrange requirement
        var testRequirement = Substitute.For<IOrganizationRequirement>();
        testRequirement
            .AuthorizeAsync(organizationId, null, Arg.Any<IProviderOrganizationContext>())
            .ReturnsForAnyArgs(false);
        var authContext = new AuthorizationHandlerContext([testRequirement], new ClaimsPrincipal(), null);

        // Act
        await sutProvider.Sut.HandleAsync(authContext);

        // Assert
        await testRequirement.Received(1).AuthorizeAsync(organizationId, null, Arg.Any<IProviderOrganizationContext>());
        Assert.False(authContext.HasSucceeded);
    }

    [Theory, BitAutoData]
    public async Task Authorizes_IfAuthorizeAsync_ReturnsTrue(SutProvider<OrganizationRequirementHandler> sutProvider, Guid organizationId)
    {
        // Arrange route values
        ArrangeRouteValues(sutProvider, organizationId.ToString());

        // Arrange requirement
        var testRequirement = Substitute.For<IOrganizationRequirement>();
        testRequirement
            .AuthorizeAsync(organizationId, null, Arg.Any<IProviderOrganizationContext>())
            .ReturnsForAnyArgs(true);
        var authContext = new AuthorizationHandlerContext([testRequirement], new ClaimsPrincipal(), null);

        // Act
        await sutProvider.Sut.HandleAsync(authContext);

        // Assert
        await testRequirement.Received(1).AuthorizeAsync(organizationId, null, Arg.Any<IProviderOrganizationContext>());
        Assert.True(authContext.HasSucceeded);
    }

    private static void ArrangeRouteValues(SutProvider<OrganizationRequirementHandler> sutProvider, string orgIdRouteValue)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["orgId"] = orgIdRouteValue;
        sutProvider.GetDependency<IHttpContextAccessor>().HttpContext = httpContext;
    }
}
