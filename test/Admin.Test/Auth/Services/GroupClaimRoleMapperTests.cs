using System.Security.Claims;
using Bit.Admin;
using Bit.Admin.Auth.Services;

namespace Admin.Test.Auth.Services;

public class GroupClaimRoleMapperTests
{
    private const string Prefix = "us-prd-cloud-adminportal-";

    [Fact]
    public void MapRoles_StripsPrefix_AndReturnsKnownRole()
    {
        var mapper = Build(Prefix);
        var principal = PrincipalWithGroups($"{Prefix}admin");

        var roles = mapper.MapRoles(principal);

        Assert.Equal(new[] { "admin" }, roles);
    }

    [Fact]
    public void MapRoles_IsCaseInsensitiveOnPrefix()
    {
        var mapper = Build(Prefix);
        var principal = PrincipalWithGroups("US-PRD-CLOUD-ADMINPORTAL-admin");

        var roles = mapper.MapRoles(principal);

        Assert.Equal(new[] { "admin" }, roles);
    }

    [Fact]
    public void MapRoles_IsCaseInsensitiveOnRoleName()
    {
        var mapper = Build(Prefix);
        var principal = PrincipalWithGroups($"{Prefix}Admin");

        var roles = mapper.MapRoles(principal);

        Assert.Equal(new[] { "admin" }, roles);
    }

    [Fact]
    public void MapRoles_SkipsGroupsThatDoNotStartWithPrefix()
    {
        var mapper = Build(Prefix);
        var principal = PrincipalWithGroups(
            "unrelated-group",
            "eu-prd-cloud-adminportal-admin",
            $"{Prefix}cs");

        var roles = mapper.MapRoles(principal);

        Assert.Equal(new[] { "cs" }, roles);
    }

    [Fact]
    public void MapRoles_SkipsSuffixesThatAreNotKnownRoles()
    {
        // A group that happens to match the prefix but has a role name we do not recognize
        // (because someone added a group in Okta that the app does not grant any permissions
        // for) is silently dropped. No permissions is the safe default.
        var mapper = Build(Prefix);
        var principal = PrincipalWithGroups($"{Prefix}superuser");

        var roles = mapper.MapRoles(principal);

        Assert.Empty(roles);
    }

    [Fact]
    public void MapRoles_DeduplicatesRepeatedRoleGroups()
    {
        var mapper = Build(Prefix);
        var principal = PrincipalWithGroups($"{Prefix}admin", $"{Prefix}admin");

        var roles = mapper.MapRoles(principal);

        Assert.Equal(new[] { "admin" }, roles);
    }

    [Fact]
    public void MapRoles_ReturnsUnionAcrossMultipleGroups()
    {
        var mapper = Build(Prefix);
        var principal = PrincipalWithGroups(
            $"{Prefix}admin",
            $"{Prefix}billing",
            $"{Prefix}cs");

        var roles = mapper.MapRoles(principal);

        Assert.Equal(new HashSet<string> { "admin", "billing", "cs" }, new HashSet<string>(roles));
    }

    [Fact]
    public void MapRoles_ReturnsEmpty_WhenPrincipalHasNoGroupsClaim()
    {
        var mapper = Build(Prefix);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("email", "you@example.com"),
        }));

        var roles = mapper.MapRoles(principal);

        Assert.Empty(roles);
    }

    [Fact]
    public void MapRoles_ReturnsEmpty_WhenPrefixUnset()
    {
        // Defensive: if the type is somehow resolved while OIDC is disabled, it should return
        // empty rather than throw. AddAdminUpstreamOidc fail-louds on an empty prefix when
        // OIDC is enabled, so this path is only hit in the OIDC-off topology.
        var mapper = Build(groupPrefix: null);
        var principal = PrincipalWithGroups("us-prd-cloud-adminportal-admin");

        var roles = mapper.MapRoles(principal);

        Assert.Empty(roles);
    }

    [Fact]
    public void MapRoles_ReturnsEmpty_WhenPrincipalIsNull()
    {
        var mapper = Build(Prefix);

        var roles = mapper.MapRoles(null);

        Assert.Empty(roles);
    }

    private static GroupClaimRoleMapper Build(string groupPrefix)
    {
        var settings = new AdminSettings
        {
            Oidc = new AdminSettings.OidcSettings
            {
                GroupPrefix = groupPrefix,
            },
        };
        return new GroupClaimRoleMapper(settings);
    }

    private static ClaimsPrincipal PrincipalWithGroups(params string[] groups)
    {
        var claims = groups.Select(g => new Claim(GroupClaimRoleMapper.GroupsClaimType, g));
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }
}
