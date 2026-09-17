using Bit.DataBoundaries.Domains;

namespace Bit.DataBoundaries.Test;

public class DomainResolverTests
{
    [Theory]
    [InlineData("src/Core/Vault/Entities/Cipher.cs", "vault")]
    [InlineData("src/Api/Dirt/Controllers/ReportsController.cs", "dirt")]
    [InlineData("bitwarden_license/src/Commercial.Core/Auth/Services/Foo.cs", "auth")]
    public void Resolve_TakesTheSegmentAfterAKnownPrefix(string path, string expected)
    {
        Assert.Equal(expected, Resolve(path).Domain);
    }

    [Theory]
    [InlineData("src/Core/AdminConsole/Entities/Organization.cs", "admin-console")]
    [InlineData("src/Core/SecretsManager/Entities/Secret.cs", "secrets-manager")]
    [InlineData("src/Core/KeyManagement/Models/Foo.cs", "key-mgmt")]
    public void Resolve_NormalizesAliasedFolderNames(string path, string expected)
    {
        Assert.Equal(expected, Resolve(path).Domain);
    }

    [Theory]
    // One case per matching mechanism, not one per config entry. The rest would restate the config.
    [InlineData("src/Identity/Startup.cs", "auth")]
    [InlineData("src/Libraries/Subscriptions.User/Foo.cs", "billing")]
    [InlineData("bitwarden_license/src/Services/Pam/Provider.cs", "pam")]
    public void Resolve_FallsBackToWholeProjectDomains(string path, string expected)
    {
        Assert.Equal(expected, Resolve(path).Domain);
    }

    [Theory]
    [InlineData("src/Admin/Billing/Views/Index.cshtml")]
    [InlineData("src/Api/AdminConsole/Controllers/OrganizationsController.cs")]
    public void Resolve_FlagsUniversalSurfaces(string path)
    {
        Assert.True(Resolve(path).IsUniversalSurface);
    }

    [Fact]
    public void Resolve_LeavesANonUniversalDomainUnflagged()
    {
        Assert.False(Resolve("src/Core/Vault/Entities/Cipher.cs").IsUniversalSurface);
    }

    [Theory]
    // Outside every prefix, and inside a prefix with no further segment to read.
    [InlineData("util/Setup/Program.cs")]
    [InlineData("src/Core/Program.cs")]
    public void Resolve_ReturnsNull_WhenNoRuleClaimsThePath(string path)
    {
        Assert.Null(Repo.Domains.Resolve(path));
    }

    [Theory]
    // Before this was gated, src/Core/Utilities resolved to a domain called "utilities", attributing
    // data to a team that does not exist.
    [InlineData("src/Core/Utilities/StaticStore.cs")]
    [InlineData("src/Api/Controllers/InfoController.cs")]
    public void Resolve_DoesNotTurnAStructuralFolderIntoADomain(string path)
    {
        Assert.Null(Repo.Domains.Resolve(path));
    }

    [Fact]
    public void Resolve_ClaimsTheStaffPortalWhenNoDomainFolderFollowsThePrefix()
    {
        var assignment = Resolve("src/Admin/Controllers/HomeController.cs");

        Assert.Equal("admin", assignment.Domain);
        Assert.True(assignment.IsUniversalSurface);
    }

    [Fact]
    public void Resolve_PrefersTheLongerOverlappingProjectPath()
    {
        var resolver = DomainResolver.FromMap(new DomainMap
        {
            ProjectDomains =
            {
                ["src/Libraries"] = "shared",
                ["src/Libraries/Invoicing"] = "billing",
            },
        });

        Assert.Equal("billing", resolver.Resolve("src/Libraries/Invoicing/Invoice.cs")?.Domain);
    }

    [Fact]
    public void Normalize_LowercasesARawFolderSegment()
    {
        Assert.Equal("vault", Repo.Domains.Normalize("Vault").Domain);
    }

    [Fact]
    public void Normalize_FlagsAUniversalSurfaceThatOnlyAppearsAsAnAlias()
    {
        Assert.True(Repo.Domains.Normalize("AdminConsole").IsUniversalSurface);
    }

    private static DomainAssignment Resolve(string path) =>
        Repo.Domains.Resolve(path) ?? throw new InvalidOperationException($"No domain resolved for '{path}'.");
}
