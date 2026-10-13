using System.Security.Claims;
using Bit.Admin.Enums;
using Bit.Admin.Services;
using Bit.Admin.Utilities;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Admin.Test.Utilities;

public class RequirePermissionAttributeTests
{
    [Fact]
    public void OnActionExecuting_Throws_WhenUserHasNoRoleClaims()
    {
        // Pins the no-permission UX: a signed-in principal without any role claims (the SSO
        // "authenticated but no mapped groups" case) hits the attribute on any gated action
        // and triggers UnauthorizedAccessException. UseExceptionHandler("/error") turns that
        // into the generic error page downstream.
        var attribute = new RequirePermissionAttribute(Permission.Org_Delete);
        var context = BuildActionContext(roleClaims: Array.Empty<string>());

        Assert.Throws<UnauthorizedAccessException>(() => attribute.OnActionExecuting(context));
    }

    [Fact]
    public void OnActionExecuting_Allows_WhenAnyRoleGrantsPermission()
    {
        var attribute = new RequirePermissionAttribute(Permission.Org_Delete);
        var context = BuildActionContext(roleClaims: new[] { "sales", "admin" });

        attribute.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void OnActionExecuting_Throws_WhenNoHeldRoleGrantsPermission()
    {
        var attribute = new RequirePermissionAttribute(Permission.Tools_PromoteAdmin);
        var context = BuildActionContext(roleClaims: new[] { "cs" });

        Assert.Throws<UnauthorizedAccessException>(() => attribute.OnActionExecuting(context));
    }

    private static ActionExecutingContext BuildActionContext(string[] roleClaims)
    {
        var identity = new ClaimsIdentity(roleClaims.Select(r => new Claim(ClaimTypes.Role, r)));
        var principal = new ClaimsPrincipal(identity);

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(new DefaultHttpContext { User = principal });

        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.SelfHosted.Returns(false);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>())
            .Build();

        var accessControlService = new AccessControlService(httpContextAccessor, configuration, globalSettings);

        var services = new ServiceCollection();
        services.AddSingleton<IAccessControlService>(accessControlService);

        var httpContext = new DefaultHttpContext
        {
            User = principal,
            RequestServices = services.BuildServiceProvider(),
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object>(),
            controller: new object());
    }
}
