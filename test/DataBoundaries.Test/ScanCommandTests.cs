using Bit.DataBoundaries.Commands;
using Bit.DataBoundaries.Domains;
using Bit.DataBoundaries.Output;

namespace Bit.DataBoundaries.Test;

public class ScanCommandTests
{
    [Fact]
    public void Execute_WritesTheSharedArtifact_ForAFullSweep()
    {
        using var repo = new TempRepo();

        var exitCode = Scan(repo, new ScanArgs());

        Assert.Equal(ExitCode.Clean, exitCode);
        Assert.True(File.Exists(repo.ReportPath));
    }

    [Fact]
    public void Execute_LeavesTheSharedArtifactByteIdentical_WhenFilteredToOneTable()
    {
        using var repo = new TempRepo();
        Scan(repo, new ScanArgs());
        var fullSweep = File.ReadAllBytes(repo.ReportPath);

        var exitCode = Scan(repo, new ScanArgs { Table = "Organization" });

        Assert.Equal(ExitCode.Clean, exitCode);
        Assert.Equal(fullSweep, File.ReadAllBytes(repo.ReportPath));
    }

    [Fact]
    public void Execute_CreatesNoSharedArtifact_WhenTheFirstRunIsFiltered()
    {
        using var repo = new TempRepo();

        var exitCode = Scan(repo, new ScanArgs { Table = "Organization" });

        Assert.Equal(ExitCode.Clean, exitCode);
        Assert.False(File.Exists(repo.ReportPath));
    }

    [Fact]
    public void Execute_IsIdempotent_AcrossTwoFullSweeps()
    {
        using var repo = new TempRepo();
        Scan(repo, new ScanArgs());
        var first = File.ReadAllBytes(repo.ReportPath);

        Scan(repo, new ScanArgs());

        Assert.Equal(first, File.ReadAllBytes(repo.ReportPath));
    }

    [Fact]
    public void Execute_RejectsAnAbsoluteOutDirectory()
    {
        using var repo = new TempRepo();
        var outside = Path.Combine(Path.GetTempPath(), $"data-boundaries-escape-{Guid.NewGuid():N}");

        var exitCode = Scan(repo, new ScanArgs { OutDirectory = outside });

        Assert.Equal(ExitCode.ToolFailure, exitCode);
        Assert.False(Directory.Exists(outside));
    }

    [Theory]
    [InlineData("../data-boundaries-escape")]
    [InlineData("util/../../data-boundaries-escape")]
    public void Execute_RejectsAnOutDirectoryThatWalksOutOfTheRepository(string outDirectory)
    {
        using var repo = new TempRepo();

        var exitCode = Scan(repo, new ScanArgs { OutDirectory = outDirectory });

        Assert.Equal(ExitCode.ToolFailure, exitCode);
        Assert.False(Directory.Exists(Path.Combine(repo.Root, "..", "data-boundaries-escape")));
    }

    [Fact]
    public void Execute_AcceptsAnOutDirectoryInsideTheRepository()
    {
        using var repo = new TempRepo();

        var exitCode = Scan(repo, new ScanArgs { OutDirectory = "build/ownership" });

        Assert.Equal(ExitCode.Clean, exitCode);
        Assert.True(File.Exists(Path.Combine(repo.Root, "build", "ownership", OwnershipReport.FileName)));
    }

    [Fact]
    public void Execute_FailsOnATableNameThatMatchesNothing()
    {
        using var repo = new TempRepo();

        Assert.Equal(ExitCode.ToolFailure, Scan(repo, new ScanArgs { Table = "NoSuchTable" }));
    }

    [Fact]
    public void Execute_FailsClosedAndWritesNothing_WhenATableFileDoesNotParse()
    {
        using var repo = new TempRepo();
        repo.Write("src/Sql/dbo/Tables/Broken.sql", "CREATE TABLE [dbo].[Broken] (\n    [Id] SELECT FROM\n);\n");

        var exitCode = Scan(repo, new ScanArgs());

        Assert.Equal(ExitCode.ToolFailure, exitCode);
        Assert.False(File.Exists(repo.ReportPath));
    }

    [Fact]
    public void Execute_ReadsTheDomainMapFromTheRepositoryRoot()
    {
        using var repo = new TempRepo();
        File.Delete(repo.DomainMap);

        Assert.Equal(ExitCode.ToolFailure, Scan(repo, new ScanArgs()));
    }

    [Fact]
    public void Execute_ReportsAMalformedDomainMapAsAToolFailure()
    {
        using var repo = new TempRepo();
        repo.Write(DomainResolver.RepoRelativePath, "{ \"segmentPrefixes\": [");

        Assert.Equal(ExitCode.ToolFailure, Scan(repo, new ScanArgs()));
    }

    [Fact]
    public void Execute_FailsWhenTheRepositoryRootIsNotOne()
    {
        var notARepository = Directory.CreateTempSubdirectory("data-boundaries-bare");
        try
        {
            var exitCode = new ScanCommand().Execute(new ScanArgs { RepoRoot = notARepository.FullName });

            Assert.Equal(ExitCode.ToolFailure, exitCode);
        }
        finally
        {
            notARepository.Delete(recursive: true);
        }
    }

    private static int Scan(TempRepo repo, ScanArgs args)
    {
        args.RepoRoot = repo.Root;
        return new ScanCommand().Execute(args);
    }
}
