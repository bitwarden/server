using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization;
using Bit.Api.Dirt.Authorization;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Dirt.Authorization;

[SutProviderCustomize]
public class UseRiskInsightsHandlerTests
{
    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_OwnerWithFeatureEnabled_Succeeds(
        Guid organizationId,
        SutProvider<UseRiskInsightsHandler> sutProvider)
    {
        // Arrange
        var organization = new Organization
        {
            Id = organizationId,
            UseRiskInsights = true
        };

        var context = new AuthorizationHandlerContext(
            new[] { new UseRiskInsightsRequirement() },
            new ClaimsPrincipal(),
            null);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.GetRouteData().Returns(new RouteData
        {
            Values = { ["organizationId"] = organizationId.ToString() }
        });

        var orgClaims = new CurrentContextOrganization
        {
            Id = organizationId,
            Type = OrganizationUserType.Owner
        };

        httpContext.User.GetCurrentContextOrganization(organizationId).Returns(orgClaims);

        sutProvider.GetDependency<IHttpContextAccessor>()
            .HttpContext.Returns(httpContext);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_AdminWithFeatureEnabled_Succeeds(
        Guid organizationId,
        SutProvider<UseRiskInsightsHandler> sutProvider)
    {
        // Arrange
        var organization = new Organization
        {
            Id = organizationId,
            UseRiskInsights = true
        };

        var context = new AuthorizationHandlerContext(
            new[] { new UseRiskInsightsRequirement() },
            new ClaimsPrincipal(),
            null);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.GetRouteData().Returns(new RouteData
        {
            Values = { ["organizationId"] = organizationId.ToString() }
        });

        var orgClaims = new CurrentContextOrganization
        {
            Id = organizationId,
            Type = OrganizationUserType.Admin
        };

        httpContext.User.GetCurrentContextOrganization(organizationId).Returns(orgClaims);

        sutProvider.GetDependency<IHttpContextAccessor>()
            .HttpContext.Returns(httpContext);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_CustomWithAccessReportsAndFeatureEnabled_Succeeds(
        Guid organizationId,
        SutProvider<UseRiskInsightsHandler> sutProvider)
    {
        // Arrange
        var organization = new Organization
        {
            Id = organizationId,
            UseRiskInsights = true
        };

        var context = new AuthorizationHandlerContext(
            new[] { new UseRiskInsightsRequirement() },
            new ClaimsPrincipal(),
            null);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.GetRouteData().Returns(new RouteData
        {
            Values = { ["organizationId"] = organizationId.ToString() }
        });

        var orgClaims = new CurrentContextOrganization
        {
            Id = organizationId,
            Type = OrganizationUserType.Custom,
            Permissions = new Permissions { AccessReports = true }
        };

        httpContext.User.GetCurrentContextOrganization(organizationId).Returns(orgClaims);

        sutProvider.GetDependency<IHttpContextAccessor>()
            .HttpContext.Returns(httpContext);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_UserWithoutAccessReports_Fails(
        Guid organizationId,
        SutProvider<UseRiskInsightsHandler> sutProvider)
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            new[] { new UseRiskInsightsRequirement() },
            new ClaimsPrincipal(),
            null);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.GetRouteData().Returns(new RouteData
        {
            Values = { ["organizationId"] = organizationId.ToString() }
        });

        var orgClaims = new CurrentContextOrganization
        {
            Id = organizationId,
            Type = OrganizationUserType.User
        };

        httpContext.User.GetCurrentContextOrganization(organizationId).Returns(orgClaims);

        sutProvider.GetDependency<IHttpContextAccessor>()
            .HttpContext.Returns(httpContext);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_FeatureDisabled_Fails(
        Guid organizationId,
        SutProvider<UseRiskInsightsHandler> sutProvider)
    {
        // Arrange
        var organization = new Organization
        {
            Id = organizationId,
            UseRiskInsights = false  // Feature disabled
        };

        var context = new AuthorizationHandlerContext(
            new[] { new UseRiskInsightsRequirement() },
            new ClaimsPrincipal(),
            null);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.GetRouteData().Returns(new RouteData
        {
            Values = { ["organizationId"] = organizationId.ToString() }
        });

        var orgClaims = new CurrentContextOrganization
        {
            Id = organizationId,
            Type = OrganizationUserType.Owner
        };

        httpContext.User.GetCurrentContextOrganization(organizationId).Returns(orgClaims);

        sutProvider.GetDependency<IHttpContextAccessor>()
            .HttpContext.Returns(httpContext);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [BitAutoData]
    public async Task HandleRequirementAsync_OrganizationNotFound_Fails(
        Guid organizationId,
        SutProvider<UseRiskInsightsHandler> sutProvider)
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            new[] { new UseRiskInsightsRequirement() },
            new ClaimsPrincipal(),
            null);

        var httpContext = Substitute.For<HttpContext>();
        httpContext.GetRouteData().Returns(new RouteData
        {
            Values = { ["organizationId"] = organizationId.ToString() }
        });

        var orgClaims = new CurrentContextOrganization
        {
            Id = organizationId,
            Type = OrganizationUserType.Owner
        };

        httpContext.User.GetCurrentContextOrganization(organizationId).Returns(orgClaims);

        sutProvider.GetDependency<IHttpContextAccessor>()
            .HttpContext.Returns(httpContext);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(null as Organization);

        // Act
        await sutProvider.Sut.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }
}
