using System.Security.Claims;
using Bit.Admin.Enums;
using Bit.Admin.Services;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace Admin.Test.Services;

public class AccessControlServiceTests
{
    [Fact]
    public void UserHasPermission_ReturnsTrue_ForSelfHostedRegardlessOfRole()
    {
        // Self-hosted admin runs single-tenant with no SSO or allowlist infrastructure;
        // whoever reached it is authorized for everything.
        var service = BuildService(selfHosted: true);

        Assert.True(service.UserHasPermission(Permission.Org_Delete));
    }

    [Fact]
    public void UserHasPermission_ReturnsFalse_WhenNoRoleClaims()
    {
        var service = BuildService(selfHosted: false, roleClaims: Array.Empty<string>());

        Assert.False(service.UserHasPermission(Permission.Org_Delete));
    }

    [Fact]
    public void UserHasPermission_ReturnsTrue_WhenAnyRoleGrantsPermission()
    {
        // sales does not have Org_Delete; admin does. The union logic should flip the gate to
        // true when any held role grants the requested permission.
        var service = BuildService(selfHosted: false, roleClaims: new[] { "sales", "admin" });

        Assert.True(service.UserHasPermission(Permission.Org_Delete));
    }

    [Fact]
    public void UserHasPermission_ReturnsFalse_WhenNoRoleGrantsPermission()
    {
        // cs is read-mostly and specifically does not have Tools_PromoteAdmin.
        var service = BuildService(selfHosted: false, roleClaims: new[] { "cs" });

        Assert.False(service.UserHasPermission(Permission.Tools_PromoteAdmin));
    }

    [Fact]
    public void UserHasPermission_IgnoresUnknownRoleClaim()
    {
        // A role claim whose value is not a key in RolePermissionMapping must not grant
        // anything. Fail-closed on unexpected values that might slip in from a misconfigured
        // IdP group.
        var service = BuildService(selfHosted: false, roleClaims: new[] { "root" });

        Assert.False(service.UserHasPermission(Permission.Org_Delete));
    }

    [Fact]
    public void UserHasPermission_IsCaseInsensitiveOnRoleName()
    {
        var service = BuildService(selfHosted: false, roleClaims: new[] { "ADMIN" });

        Assert.True(service.UserHasPermission(Permission.Org_Delete));
    }

    private static AccessControlService BuildService(bool selfHosted, string[] roleClaims = null)
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        if (roleClaims is not null)
        {
            var identity = new ClaimsIdentity(roleClaims.Select(r => new Claim(ClaimTypes.Role, r)));
            httpContextAccessor.HttpContext.Returns(new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            });
        }

        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.SelfHosted.Returns(selfHosted);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>())
            .Build();

        return new AccessControlService(httpContextAccessor, configuration, globalSettings);
    }
}
