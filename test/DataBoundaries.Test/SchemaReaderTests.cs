using Bit.DataBoundaries.Schema;

namespace Bit.DataBoundaries.Test;

public class SchemaReaderTests
{
    /// <summary>
    /// Table files still in the unclassified src/Sql/dbo/Tables folder, where CODEOWNERS resolves them
    /// to dbops and no product code is accountable. Lower this when a file moves into a domain folder.
    /// </summary>
    private const int UnclassifiedTableFileHighWaterMark = 27;

    // Floors, not exact totals: this project runs in the repo-wide `dotnet test ./test` sweep, so a
    // pinned global count would fail any PR that merely adds a table or a column.
    private const int MinimumTableFiles = 60;
    private const int MinimumOrganizationColumns = 60;

    [Fact]
    public void Read_ProducesNoParseErrors_ForTheCommittedSchema()
    {
        Assert.Empty(Repo.Schema.Errors);
    }

    [Fact]
    public void Read_FindsTheTableFilesUnderTheSchemaRoot()
    {
        Assert.True(
            Repo.Schema.Tables.Length >= MinimumTableFiles,
            $"Found {Repo.Schema.Tables.Length} tables under {SchemaReader.SchemaRoot}, " +
            $"fewer than the {MinimumTableFiles} this repository is known to have.");
    }

    [Fact]
    public void Read_FindsNoMoreUnclassifiedTableFilesThanTheHighWaterMark()
    {
        var unclassified = Repo.Schema.Tables.Count(t => SchemaPath.IsUnclassifiedTableFile(t.SchemaFile));

        Assert.True(
            unclassified <= UnclassifiedTableFileHighWaterMark,
            $"{unclassified} table files sit in src/Sql/dbo/Tables/, above the high-water mark of " +
            $"{UnclassifiedTableFileHighWaterMark}. A new table belongs in a domain folder.");
    }

    [Fact]
    public void Read_ExtractsEveryColumn_FromOrganization()
    {
        var columns = Repo.Table("Organization").Columns;

        Assert.True(
            columns.Length >= MinimumOrganizationColumns,
            $"Found {columns.Length} Organization columns, fewer than the " +
            $"{MinimumOrganizationColumns} this repository is known to have.");
    }

    [Fact]
    public void Read_ExtractsColumnsDeclaredWithAndWithoutDefaultConstraints()
    {
        var columns = Repo.Table("Organization").Columns;

        Assert.Contains("SecretsManagerBeta", columns);
        Assert.Contains("MaxStorageGbIncreased", columns);
    }

    [Fact]
    public void Read_PreservesDeclarationOrder()
    {
        var columns = Repo.Table("Organization").Columns;

        Assert.Equal("Id", columns[0]);
        Assert.Equal("Identifier", columns[1]);
    }

    [Fact]
    public void Read_PopulatesTableIdentity()
    {
        var organization = Repo.Table("Organization");

        Assert.Equal("dbo", organization.Schema);
        Assert.Equal("src/Sql/dbo/Tables/Organization.sql", organization.SchemaFile);
    }

    [Fact]
    public void Read_IgnoresSqlFilesOutsideTablesFolders()
    {
        Assert.DoesNotContain(
            Repo.Schema.Tables,
            t => t.SchemaFile.Contains("/Stored Procedures/", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_ThrowsWhenTheSchemaRootIsMissing()
    {
        var empty = Directory.CreateTempSubdirectory("data-boundaries-test");
        try
        {
            Assert.Throws<DirectoryNotFoundException>(() => SchemaReader.Read(empty.FullName));
        }
        finally
        {
            empty.Delete(recursive: true);
        }
    }
}
