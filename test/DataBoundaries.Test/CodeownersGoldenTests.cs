namespace Bit.DataBoundaries.Test;

/// <summary>
/// Resolutions against the committed .github/CODEOWNERS. The last-match-wins mechanism is pinned with
/// inline fixtures in <see cref="CodeownersResolverTests"/>, so adding a rule cannot fail this project.
/// </summary>
public class CodeownersGoldenTests
{
    [Fact]
    public void DomainSchemaFolder_ResolvesToAProductTeam_NotDatabaseOperations()
    {
        // The exact handle is not asserted. Renaming or reordering a domain rule is routine; losing
        // product ownership of a domain folder is the Lane 3 regression worth failing on.
        var owners = Repo.Codeowners.Resolve("src/Sql/dbo/Vault/Tables/Cipher.sql");

        Assert.NotEmpty(owners);
        Assert.DoesNotContain("@bitwarden/dept-dbops", owners);
    }

    [Fact]
    public void PamSchemaFile_IsClawedBackByDatabaseOperations()
    {
        // Asserted exactly, because it tracks a decision rather than an accident: src/Sql/dbo/Pam is
        // listed after **/Pam* on purpose. A failure here means that decision changed.
        Assert.Equal(
            ["@bitwarden/dept-dbops"],
            Repo.Codeowners.Resolve("src/Sql/dbo/Pam/Tables/AccessLease.sql"));
    }

    [Fact]
    public void LockFile_HasNoOwner()
    {
        Assert.Empty(Repo.Codeowners.Resolve("src/Core/packages.lock.json"));
    }

    [Fact]
    public void SecretsManagerSource_HasNoOwner()
    {
        // Documents a real gap: no SecretsManager rule exists, and no team-secrets-manager handle
        // exists anywhere in the file. When one is added, update the expectation. Do not delete this.
        Assert.Empty(Repo.Codeowners.Resolve("src/Core/SecretsManager/Entities/Secret.cs"));
    }
}
