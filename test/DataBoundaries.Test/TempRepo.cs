using System.ComponentModel;
using System.Diagnostics;
using Bit.DataBoundaries.Commands;
using Bit.DataBoundaries.Domains;
using Bit.DataBoundaries.Output;
using Bit.DataBoundaries.Ownership;

namespace Bit.DataBoundaries.Test;

/// <summary>
/// A throwaway directory shaped like the repository: the three inputs scan reads, plus the .git marker
/// RepoLocator looks for. The marker is a plain file, which is what a git worktree has, so no git
/// command is involved unless a test opts into <see cref="TryInitGitRepository"/>.
/// </summary>
internal sealed class TempRepo : IDisposable
{
    /// <summary>
    /// Supplied per invocation so the fixture cannot pick up the developer's identity or signing key.
    /// </summary>
    private static readonly string[] _hermeticGitConfig =
    [
        "user.email=fixture@example.invalid",
        "user.name=DataBoundaries Fixture",
        "commit.gpgsign=false",
    ];

    public TempRepo()
    {
        Root = Directory.CreateTempSubdirectory("data-boundaries-repo").FullName;

        File.WriteAllText(Path.Combine(Root, ".git"), "gitdir: fixture, never resolved\n");
        Write(".github/CODEOWNERS",
            """
            src/Sql/** @bitwarden/dept-dbops
            **/Vault @bitwarden/team-vault-dev
            src/Sql/dbo/Retired @bitwarden/dept-dbops

            """);
        Write(
            DomainResolver.RepoRelativePath,
            File.ReadAllText(Path.Combine(Repo.Root, DomainResolver.RepoRelativePath)));
        Write(GrandfatheredTables.RepoRelativePath, "# fixture\nsrc/Sql/dbo/Tables/Organization.sql\n");
        Write("src/Sql/dbo/Tables/Organization.sql", Table("Organization"));
        Write("src/Sql/dbo/Vault/Tables/Cipher.sql", Table("Cipher"));
    }

    public string Root { get; }

    public string ReportPath => Path.Combine(Root, ScanArgs.DefaultOutDirectory, OwnershipReport.FileName);

    public string DomainMap => Path.Combine(Root, DomainResolver.RepoRelativePath);

    public string Allowlist => Path.Combine(Root, GrandfatheredTables.RepoRelativePath);

    public string ChangedFiles(params string[] repoRelativePaths)
    {
        var path = Path.Combine(Root, "changed-files.txt");
        File.WriteAllText(path, string.Join('\n', repoRelativePaths) + "\n");
        return path;
    }

    public string BaselineAllowlist(params string[] entries)
    {
        var path = Path.Combine(Root, "baseline-allowlist.txt");
        File.WriteAllText(path, string.Join('\n', entries) + "\n");
        return path;
    }

    public void Write(string repoRelativePath, string content)
    {
        var path = Path.Combine(Root, repoRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Replaces the .git marker with a real repository whose HEAD holds everything written so far, so a
    /// command that falls back to git sees a working tree. False when git is unavailable.
    /// </summary>
    public bool TryInitGitRepository()
    {
        File.Delete(Path.Combine(Root, ".git"));

        return Git("init", "--quiet")
            && Git("add", "--all")
            && Git("commit", "--quiet", "--message", "fixture");
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    private bool Git(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var setting in _hermeticGitConfig)
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(setting);
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static string Table(string name) =>
        $"CREATE TABLE [dbo].[{name}] (\n    [Id] UNIQUEIDENTIFIER NOT NULL\n);\n";
}
