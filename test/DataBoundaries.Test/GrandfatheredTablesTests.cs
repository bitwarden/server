using Bit.DataBoundaries.Ownership;
using Bit.DataBoundaries.Schema;

namespace Bit.DataBoundaries.Test;

public class GrandfatheredTablesTests
{
    private const string DatabasePlatformOwner = "@bitwarden/dept-dbops";

    [Fact]
    public void Parse_IgnoresCommentsAndBlankLines()
    {
        var entries = GrandfatheredTables.Parse(
            ["# header", "", "  ", "src/Sql/dbo/Tables/User.sql", "  src/Sql/dbo/Tables/Group.sql  "]);

        Assert.Equal(
            ["src/Sql/dbo/Tables/Group.sql", "src/Sql/dbo/Tables/User.sql"],
            entries.OrderBy(entry => entry, StringComparer.Ordinal));
    }

    [Fact]
    public void FindAddedEntries_ReportsOnlyWhatTheChangeAdded()
    {
        var current = GrandfatheredTables.Parse(["a.sql", "b.sql", "c.sql"]);
        var baseline = GrandfatheredTables.Parse(["a.sql", "c.sql"]);

        Assert.Equal(["b.sql"], GrandfatheredTables.FindAddedEntries(current, baseline));
    }

    [Fact]
    public void FindAddedEntries_TreatsRemovalAsNoViolation()
    {
        var current = GrandfatheredTables.Parse(["a.sql"]);
        var baseline = GrandfatheredTables.Parse(["a.sql", "b.sql"]);

        Assert.Empty(GrandfatheredTables.FindAddedEntries(current, baseline));
    }

    [Fact]
    public void FromRepo_ListsOnlyTableFiles()
    {
        Assert.All(
            GrandfatheredTables.FromRepo(Repo.Root),
            entry => Assert.True(SchemaPath.IsTableFile(entry), $"{entry} is not a table file."));
    }

    [Fact]
    public void TheCommittedAllowlist_CoversEveryTableWithoutAProductOwner()
    {
        var committed = Committed();
        var unowned = Repo.Schema.Tables
            .Select(table => table.SchemaFile)
            .Where(HasNoProductOwner);

        // Missing coverage would make check --sql-ownership fail a PR for a table it did not introduce.
        Assert.All(unowned, file => Assert.True(
            committed.Contains(file),
            $"{file} has no product-team owner and no entry in {GrandfatheredTables.RepoRelativePath}, " +
            $"so check --sql-ownership fails any change to it. Move it to " +
            $"{SchemaReader.SchemaRoot}/<Domain>/Tables/, or give its domain folder a CODEOWNERS rule " +
            "naming the product team."));
    }

    [Fact]
    public void TheCommittedAllowlist_CarriesNoEntryForATableThatIsGone()
    {
        // A stale entry is not a policy failure, but it hides a shrink that already happened.
        Assert.All(Committed(), entry => Assert.True(
            File.Exists(Path.Combine(Repo.Root, entry)),
            $"{entry} is grandfathered but no longer exists. Delete its line."));
    }

    private static IReadOnlySet<string> Committed() =>
        GrandfatheredTables.Parse(File.ReadAllLines(
            Path.Combine(Repo.Root, GrandfatheredTables.RepoRelativePath)));

    /// <summary>
    /// The three shapes check --sql-ownership rejects: no domain folder, no CODEOWNERS pattern at all,
    /// or only the database platform team, which owns the platform rather than what the rows mean.
    /// </summary>
    private static bool HasNoProductOwner(string schemaFile)
    {
        if (!SchemaPath.IsTableFile(schemaFile))
        {
            return false;
        }

        if (SchemaPath.IsUnclassifiedTableFile(schemaFile))
        {
            return true;
        }

        var owners = Repo.Codeowners.Resolve(schemaFile);
        return owners.IsEmpty || owners is [DatabasePlatformOwner];
    }
}
