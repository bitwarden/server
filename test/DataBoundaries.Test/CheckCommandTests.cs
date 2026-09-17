using Bit.DataBoundaries.Commands;
using Bit.DataBoundaries.Ownership;

namespace Bit.DataBoundaries.Test;

public class CheckCommandTests
{
    [Fact]
    public void SqlOwnership_FailsForARootFolderTableTheAllowlistDoesNotCover()
    {
        using var repo = new TempRepo();
        repo.Write("src/Sql/dbo/Tables/NewThing.sql", "CREATE TABLE [dbo].[NewThing] ( [Id] INT );");

        var exitCode = Check(repo, "src/Sql/dbo/Tables/NewThing.sql");

        Assert.Equal(ExitCode.PolicyViolation, exitCode);
    }

    [Fact]
    public void SqlOwnership_PassesForARootFolderTableTheAllowlistCovers()
    {
        using var repo = new TempRepo();

        // Organization resolves only to dept-dbops through src/Sql/**, and is grandfathered.
        var exitCode = Check(repo, "src/Sql/dbo/Tables/Organization.sql");

        Assert.Equal(ExitCode.Clean, exitCode);
    }

    [Fact]
    public void SqlOwnership_PassesForADomainFolderTableWithAProductOwner()
    {
        using var repo = new TempRepo();

        var exitCode = Check(repo, "src/Sql/dbo/Vault/Tables/Cipher.sql");

        Assert.Equal(ExitCode.Clean, exitCode);
    }

    [Fact]
    public void SqlOwnership_FailsForADomainFolderTableResolvingOnlyToTheDatabasePlatformTeam()
    {
        using var repo = new TempRepo();
        repo.Write("src/Sql/dbo/Retired/Tables/Old.sql", "CREATE TABLE [dbo].[Old] ( [Id] INT );");

        var exitCode = Check(repo, "src/Sql/dbo/Retired/Tables/Old.sql");

        Assert.Equal(ExitCode.PolicyViolation, exitCode);
    }

    [Fact]
    public void SqlOwnership_FailsForATableFileNoPatternClaims()
    {
        using var repo = new TempRepo();
        repo.Write(".github/CODEOWNERS", "src/Sql/dbo/Vault/** @bitwarden/team-vault-dev\n");
        repo.Write("src/Sql/dbo/Retired/Tables/Old.sql", "CREATE TABLE [dbo].[Old] ( [Id] INT );");

        var exitCode = Check(repo, "src/Sql/dbo/Retired/Tables/Old.sql");

        Assert.Equal(ExitCode.PolicyViolation, exitCode);
    }

    [Fact]
    public void SqlOwnership_PassesWhenNoChangedPathIsATableFile()
    {
        using var repo = new TempRepo();

        var exitCode = Check(repo, "src/Core/Vault/Services/CipherService.cs", "README.md");

        Assert.Equal(ExitCode.Clean, exitCode);
    }

    [Fact]
    public void SqlOwnership_IgnoresSqlFilesOutsideTablesFolders()
    {
        using var repo = new TempRepo();

        var exitCode = Check(repo, "src/Sql/dbo/Stored Procedures/Organization_Create.sql");

        Assert.Equal(ExitCode.Clean, exitCode);
    }

    [Fact]
    public void SqlOwnership_IgnoresBlankLinesInTheChangedFileList()
    {
        using var repo = new TempRepo();

        var exitCode = Check(repo, "", "src/Sql/dbo/Vault/Tables/Cipher.sql", "", "   ");

        Assert.Equal(ExitCode.Clean, exitCode);
    }

    [Fact]
    public void AllowlistGrowth_PassesWhenAnEntryWasRemoved()
    {
        using var repo = new TempRepo();
        var baseline = repo.BaselineAllowlist(
            "src/Sql/dbo/Tables/Organization.sql", "src/Sql/dbo/Tables/User.sql");

        var exitCode = new CheckCommand().Execute(new CheckArgs
        {
            RepoRoot = repo.Root,
            SqlOwnership = true,
            BaselineAllowlist = baseline,
            ChangedFiles = repo.ChangedFiles(GrandfatheredTables.RepoRelativePath),
        });

        Assert.Equal(ExitCode.Clean, exitCode);
    }

    [Fact]
    public void AllowlistGrowth_FailsWhenAnEntryWasAdded()
    {
        using var repo = new TempRepo();
        repo.Write(
            GrandfatheredTables.RepoRelativePath,
            "src/Sql/dbo/Tables/Organization.sql\nsrc/Sql/dbo/Tables/SneakedIn.sql\n");
        var baseline = repo.BaselineAllowlist("src/Sql/dbo/Tables/Organization.sql");

        var exitCode = new CheckCommand().Execute(new CheckArgs
        {
            RepoRoot = repo.Root,
            SqlOwnership = true,
            BaselineAllowlist = baseline,
            ChangedFiles = repo.ChangedFiles(GrandfatheredTables.RepoRelativePath),
        });

        Assert.Equal(ExitCode.PolicyViolation, exitCode);
    }

    [Fact]
    public void AllowlistGrowth_IsNotCheckedWhenTheListItselfDidNotChange()
    {
        using var repo = new TempRepo();
        repo.Write(
            GrandfatheredTables.RepoRelativePath,
            "src/Sql/dbo/Tables/Organization.sql\nsrc/Sql/dbo/Tables/SneakedIn.sql\n");

        var exitCode = new CheckCommand().Execute(new CheckArgs
        {
            RepoRoot = repo.Root,
            SqlOwnership = true,
            BaselineAllowlist = repo.BaselineAllowlist("src/Sql/dbo/Tables/Organization.sql"),
            ChangedFiles = repo.ChangedFiles("src/Sql/dbo/Vault/Tables/Cipher.sql"),
        });

        Assert.Equal(ExitCode.Clean, exitCode);
    }

    [Fact]
    public void Staleness_PassesForAMapThatMatchesAFreshScan()
    {
        using var repo = new TempRepo();
        new ScanCommand().Execute(new ScanArgs { RepoRoot = repo.Root });

        var exitCode = new CheckCommand().Execute(new CheckArgs { RepoRoot = repo.Root, Staleness = true });

        Assert.Equal(ExitCode.Clean, exitCode);
    }

    [Fact]
    public void Staleness_FailsForAHandEditedMap()
    {
        using var repo = new TempRepo();
        new ScanCommand().Execute(new ScanArgs { RepoRoot = repo.Root });
        File.WriteAllText(
            repo.ReportPath, File.ReadAllText(repo.ReportPath).Replace("\"columnCount\": 1", "\"columnCount\": 9"));

        var exitCode = new CheckCommand().Execute(new CheckArgs { RepoRoot = repo.Root, Staleness = true });

        Assert.Equal(ExitCode.PolicyViolation, exitCode);
    }

    [Fact]
    public void Staleness_FailsWhenTheMapWasNeverGenerated()
    {
        using var repo = new TempRepo();

        var exitCode = new CheckCommand().Execute(new CheckArgs { RepoRoot = repo.Root, Staleness = true });

        Assert.Equal(ExitCode.PolicyViolation, exitCode);
    }

    [Fact]
    public void NeitherFlag_RunsTheStalenessCheck()
    {
        using var repo = new TempRepo();

        // The map was never generated, so the staleness half must fail even though ownership is clean.
        var exitCode = new CheckCommand().Execute(new CheckArgs
        {
            RepoRoot = repo.Root,
            ChangedFiles = repo.ChangedFiles("src/Sql/dbo/Vault/Tables/Cipher.sql"),
        });

        Assert.Equal(ExitCode.PolicyViolation, exitCode);
    }

    [Fact]
    public void NeitherFlag_RunsTheOwnershipCheck()
    {
        using var repo = new TempRepo();
        repo.Write("src/Sql/dbo/Tables/NewThing.sql", "CREATE TABLE [dbo].[NewThing] ( [Id] INT );");

        // Scanning after the write leaves the map fresh, so only the ownership half can fail.
        new ScanCommand().Execute(new ScanArgs { RepoRoot = repo.Root });

        var exitCode = new CheckCommand().Execute(new CheckArgs
        {
            RepoRoot = repo.Root,
            ChangedFiles = repo.ChangedFiles("src/Sql/dbo/Tables/NewThing.sql"),
        });

        Assert.Equal(ExitCode.PolicyViolation, exitCode);
    }

    [Fact]
    public void SqlOwnership_IgnoresTheOldPathOfAnUnstagedMove()
    {
        using var repo = new TempRepo();
        Assert.SkipUnless(
            repo.TryInitGitRepository(), "git is unavailable, so the working-tree fallback cannot run.");

        // The shape of an agent edit: write the destination, delete the source, stage neither.
        File.Move(
            Path.Combine(repo.Root, "src/Sql/dbo/Tables/Organization.sql"),
            Path.Combine(repo.Root, "src/Sql/dbo/Vault/Tables/Organization.sql"));
        repo.Write(GrandfatheredTables.RepoRelativePath, "# fixture\n");

        var exitCode = new CheckCommand().Execute(new CheckArgs { RepoRoot = repo.Root, SqlOwnership = true });

        Assert.Equal(ExitCode.Clean, exitCode);
    }

    [Fact]
    public void FailsAsAToolError_WhenTheChangedFileListIsMissing()
    {
        using var repo = new TempRepo();

        var exitCode = new CheckCommand().Execute(new CheckArgs
        {
            RepoRoot = repo.Root,
            SqlOwnership = true,
            ChangedFiles = Path.Combine(repo.Root, "no-such-list.txt"),
        });

        Assert.Equal(ExitCode.ToolFailure, exitCode);
    }

    [Fact]
    public void FailsAsAToolError_WhenTheAllowlistIsMissing()
    {
        using var repo = new TempRepo();
        File.Delete(repo.Allowlist);

        var exitCode = Check(repo, "src/Sql/dbo/Vault/Tables/Cipher.sql");

        Assert.Equal(ExitCode.ToolFailure, exitCode);
    }

    [Fact]
    public void FailsAsAToolError_WhenTheWorkingTreeCannotBeRead()
    {
        // No --changed-files and no real repository, so the git fallback cannot answer.
        using var repo = new TempRepo();

        var exitCode = new CheckCommand().Execute(new CheckArgs { RepoRoot = repo.Root, SqlOwnership = true });

        Assert.Equal(ExitCode.ToolFailure, exitCode);
    }

    private static int Check(TempRepo repo, params string[] changedPaths) =>
        new CheckCommand().Execute(new CheckArgs
        {
            RepoRoot = repo.Root,
            SqlOwnership = true,
            ChangedFiles = repo.ChangedFiles(changedPaths),
        });
}
