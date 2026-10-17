using System.Security.Claims;
using Bit.Admin;
using Bit.Admin.Auth.Services;
using Microsoft.AspNetCore.Identity;

namespace Admin.Test.Auth.Services;

public class SsoSignInPrincipalBuilderTests
{
    [Fact]
    public void Build_PrincipalIsAuthenticated_WithApplicationSchemeAuthenticationType()
    {
        // IsAuthenticated depends on AuthenticationType being non-null. The application cookie
        // middleware expects any principal written via HttpContext.SignInAsync to resolve as
        // authenticated.
        var builder = BuildBuilder();

        var principal = builder.Build("you@example.com", PrincipalWithGroups());

        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Equal(IdentityConstants.ApplicationScheme, principal.Identity.AuthenticationType);
    }

    [Fact]
    public void Build_StampsEmailAndNameClaims()
    {
        var builder = BuildBuilder();

        var principal = builder.Build("you@example.com", PrincipalWithGroups());

        Assert.Equal("you@example.com", principal.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal("you@example.com", principal.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal("you@example.com", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public void Build_StampsRoleClaims_FromMappedGroups()
    {
        var builder = BuildBuilder();
        var external = PrincipalWithGroups(
            "us-prd-cloud-adminportal-admin",
            "us-prd-cloud-adminportal-billing");

        var principal = builder.Build("you@example.com", external);

        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        Assert.Contains("admin", roles);
        Assert.Contains("billing", roles);
    }

    [Fact]
    public void Build_StampsNoRoleClaims_WhenNoGroupsMap()
    {
        // Mirror of the controller contract: a signed-in SSO user with no mapped groups still
        // gets a valid principal, but holds no role claims. AccessControlService.UserHasPermission
        // denies every gated action from there.
        var builder = BuildBuilder();
        var external = PrincipalWithGroups("unrelated-group");

        var principal = builder.Build("you@example.com", external);

        Assert.Empty(principal.FindAll(ClaimTypes.Role));
    }

    private static SsoSignInPrincipalBuilder BuildBuilder()
    {
        var mapper = new GroupClaimRoleMapper(new AdminSettings
        {
            Oidc = new AdminSettings.OidcSettings { GroupPrefix = "us-prd-cloud-adminportal-" },
        });
        return new SsoSignInPrincipalBuilder(mapper);
    }

    private static ClaimsPrincipal PrincipalWithGroups(params string[] groups)
    {
        var claims = groups.Select(g => new Claim(GroupClaimRoleMapper.GroupsClaimType, g));
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }
}
