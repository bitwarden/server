using Bit.DataBoundaries.Ownership;

namespace Bit.DataBoundaries.Test;

public class CodeownersResolverTests
{
    [Theory]
    // Later wins over an earlier broad rule.
    [InlineData("src/Sql/** @team-a\n**/Vault @team-b", "@team-b")]
    // Later wins even when the earlier rule is more specific, which is the mistake a
    // most-specific-wins implementation would make.
    [InlineData("src/Sql/dbo/Vault/Tables/Cipher.sql @team-a\nsrc/Sql/** @team-b", "@team-b")]
    // The shape .github/CODEOWNERS has around src/Sql/dbo/Pam: a floating rule captures the path
    // and an explicit rule below it reclaims the path.
    [InlineData("src/Sql/** @team-a\n**/Vault @team-b\nsrc/Sql/dbo/Vault @team-c", "@team-c")]
    public void Resolve_PrefersTheLastMatchingRule(string codeowners, string expected)
    {
        var resolver = CodeownersResolver.Parse(codeowners);

        Assert.Equal([expected], resolver.Resolve("src/Sql/dbo/Vault/Tables/Cipher.sql"));
    }

    [Fact]
    public void Resolve_StripsTrailingComments()
    {
        var resolver = CodeownersResolver.Parse("src/Core/** @team-a # owns the core project");

        Assert.Equal(["@team-a"], resolver.Resolve("src/Core/Entities/User.cs"));
    }

    [Fact]
    public void Resolve_SkipsFullLineComments()
    {
        var resolver = CodeownersResolver.Parse(
            """
            # src/Core/** @team-a
            src/Api/** @team-b
            """);

        Assert.Empty(resolver.Resolve("src/Core/Entities/User.cs"));
    }

    [Fact]
    public void Resolve_ReturnsNoOwners_WhenALaterPatternHasNoOwners()
    {
        var resolver = CodeownersResolver.Parse(
            """
            src/Core/** @team-a
            **/packages.lock.json
            """);

        Assert.Empty(resolver.Resolve("src/Core/packages.lock.json"));
    }

    [Fact]
    public void Resolve_KeepsEveryOwnerOnAMultiOwnerLine()
    {
        var resolver = CodeownersResolver.Parse("**/Vault/AuthorizationHandlers @team-a @team-b");

        Assert.Equal(
            ["@team-a", "@team-b"],
            resolver.Resolve("src/Core/Vault/AuthorizationHandlers/Handler.cs"));
    }

    [Fact]
    public void Resolve_AnchorsAPatternContainingASlashToTheRepositoryRoot()
    {
        var resolver = CodeownersResolver.Parse("src/Identity @team-a");

        Assert.Empty(resolver.Resolve("bitwarden_license/src/Identity/Startup.cs"));
    }

    [Fact]
    public void Resolve_MatchesAPatternWithoutASlashAtAnyDepth()
    {
        var resolver = CodeownersResolver.Parse("Directory.Build.props @team-a");

        Assert.Equal(["@team-a"], resolver.Resolve("src/Core/Directory.Build.props"));
    }

    [Fact]
    public void Resolve_MatchesADoubleStarPrefixAtAnyDepth()
    {
        var resolver = CodeownersResolver.Parse("**/Auth @team-a");

        Assert.Equal(["@team-a"], resolver.Resolve("bitwarden_license/src/Commercial.Core/Auth/Models/Foo.cs"));
    }

    [Fact]
    public void Resolve_TransfersDirectoryOwnershipToItsContents()
    {
        var resolver = CodeownersResolver.Parse(".claude/ @team-a");

        Assert.Equal(["@team-a"], resolver.Resolve(".claude/settings.json"));
    }

    [Fact]
    public void Resolve_MatchesCaseSensitively()
    {
        var resolver = CodeownersResolver.Parse("**/*billing* @team-a");

        Assert.Empty(resolver.Resolve("src/Core/Billing/BillingService.cs"));
    }

    [Fact]
    public void Resolve_MatchesASingleStarWithinOneSegmentOnly()
    {
        var resolver = CodeownersResolver.Parse("src/*/Startup.cs @team-a");

        Assert.Empty(resolver.Resolve("src/Core/Nested/Startup.cs"));
    }

    [Fact]
    public void ResolveRule_CitesTheLineOfTheWinningPattern()
    {
        var resolver = CodeownersResolver.Parse(
            """
            # ownership starts below
            src/Sql/** @team-a
            **/Vault @team-b
            """);

        Assert.Equal(3, resolver.ResolveRule("src/Sql/dbo/Vault/Tables/Cipher.sql")?.LineNumber);
    }

    [Fact]
    public void ResolveRule_ReturnsNull_WhenNothingMatches()
    {
        var resolver = CodeownersResolver.Parse("src/Api/** @team-a");

        Assert.Null(resolver.ResolveRule("util/Setup/Program.cs"));
    }

    [Fact]
    public void FindUnmatchedPatterns_ReportsPatternsThatGuardNothing()
    {
        var resolver = CodeownersResolver.Parse(
            """
            src/Api/** @team-a
            src/Removed/** @team-b
            """);

        Assert.Equal(["src/Removed/**"], resolver.FindUnmatchedPatterns(["src/Api/Startup.cs"]));
    }
}
