using Bit.DataBoundaries.Output;
using Bit.DataBoundaries.Schema;

namespace Bit.DataBoundaries.Test;

public class OwnershipReportTests
{
    [Fact]
    public void Build_TakesTheDomainFromAKnownSchemaFolder()
    {
        Assert.Equal("vault", Domain("src/Sql/dbo/Vault/Tables/Cipher.sql"));
    }

    [Theory]
    // A folder naming no real domain would otherwise be minted into the map, attributing data to a
    // team that does not exist.
    [InlineData("src/Sql/dbo/Retired/Tables/Old.sql")]
    [InlineData("src/Sql/dbo/Reporting/Tables/Usage.sql")]
    public void Build_LeavesTheDomainNull_ForASchemaFolderThatIsNotADomain(string schemaFile)
    {
        Assert.Null(Domain(schemaFile));
    }

    [Fact]
    public void Build_LeavesTheDomainNull_ForTheUnclassifiedRoot()
    {
        Assert.Null(Domain("src/Sql/dbo/Tables/Organization.sql"));
    }

    private static string? Domain(string schemaFile)
    {
        var table = new SchemaTable("dbo", "Anything", schemaFile, ["Id"]);
        var report = OwnershipReport.Build([table], Repo.Codeowners, Repo.Domains);

        return Assert.Single(report.Tables).SchemaDomain;
    }
}
