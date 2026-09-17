using Bit.DataBoundaries.Schema;

namespace Bit.DataBoundaries.Test;

public class SchemaPathTests
{
    [Theory]
    [InlineData("src/Sql/dbo/Tables/Organization.sql")]
    [InlineData("src/Sql/dbo/Vault/Tables/Cipher.sql")]
    public void IsTableFile_AcceptsTableFilesWithAndWithoutADomainFolder(string path)
    {
        Assert.True(SchemaPath.IsTableFile(path));
    }

    [Theory]
    [InlineData("src/Sql/dbo/Stored Procedures/Organization_Create.sql")]
    [InlineData("src/Sql/dbo/Views/OrganizationView.sql")]
    [InlineData("src/Sql/dbo/Tables/Nested/Organization.sql")]
    [InlineData("src/Sql/dbo/Tables/Organization.txt")]
    [InlineData("util/Migrator/DbScripts/2026-01-01_00_Thing.sql")]
    [InlineData("src/Core/Vault/Entities/Cipher.cs")]
    public void IsTableFile_RejectsEverythingElse(string path)
    {
        Assert.False(SchemaPath.IsTableFile(path));
    }

    [Fact]
    public void IsUnclassifiedTableFile_AcceptsOnlyTheRootTablesFolder()
    {
        Assert.True(SchemaPath.IsUnclassifiedTableFile("src/Sql/dbo/Tables/Organization.sql"));
        Assert.False(SchemaPath.IsUnclassifiedTableFile("src/Sql/dbo/Vault/Tables/Cipher.sql"));
    }
}
