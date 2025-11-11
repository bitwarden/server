using Bit.Seeder.Migration.Utils;
using Xunit;

namespace Bit.Seeder.Test.Migration.Utils;

public class IdentifierValidatorTests
{
    [Fact]
    public void IsValid_ValidIdentifier_ReturnsTrue()
    {
        // Arrange
        var validIdentifier = "MyTable123";

        // Act
        var result = IdentifierValidator.IsValid(validIdentifier);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValid_ValidIdentifierStartingWithUnderscore_ReturnsTrue()
    {
        // Arrange
        var validIdentifier = "_MyTable";

        // Act
        var result = IdentifierValidator.IsValid(validIdentifier);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_NullOrWhiteSpace_ReturnsFalse(string? identifier)
    {
        // Act
        var result = IdentifierValidator.IsValid(identifier);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValid_TooLongIdentifier_ReturnsFalse()
    {
        // Arrange - create identifier longer than 128 characters
        var tooLongIdentifier = new string('a', 129);

        // Act
        var result = IdentifierValidator.IsValid(tooLongIdentifier);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("Table-Name")]
    [InlineData("Table Name")]
    [InlineData("Table.Name")]
    [InlineData("Table;Name")]
    [InlineData("Table'Name")]
    [InlineData("Table\"Name")]
    [InlineData("Table*Name")]
    [InlineData("Table/Name")]
    [InlineData("Table\\Name")]
    [InlineData("123Table")]
    public void IsValid_InvalidCharactersOrStartingWithNumber_ReturnsFalse(string identifier)
    {
        // Act
        var result = IdentifierValidator.IsValid(identifier);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("SELECT")]
    [InlineData("DROP")]
    [InlineData("DELETE")]
    [InlineData("INSERT")]
    [InlineData("UPDATE")]
    [InlineData("EXEC")]
    [InlineData("EXECUTE")]
    public void IsValid_SqlReservedKeyword_WithRestrictiveMode_ReturnsFalse(string keyword)
    {
        // Act
        var result = IdentifierValidator.IsValid(keyword, useRestrictiveMode: true);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("SELECT")]
    [InlineData("DROP")]
    [InlineData("DELETE")]
    public void IsValid_SqlReservedKeyword_WithoutRestrictiveMode_ReturnsTrue(string keyword)
    {
        // Act - without restrictive mode, these pass the regex but are still SQL keywords
        var result = IdentifierValidator.IsValid(keyword, useRestrictiveMode: false);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("_LeadingUnderscore")]
    [InlineData("_table")]
    public void IsValid_LeadingUnderscore_WithRestrictiveMode_ReturnsFalse(string identifier)
    {
        // Act
        var result = IdentifierValidator.IsValid(identifier, useRestrictiveMode: true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateOrThrow_ValidIdentifier_DoesNotThrow()
    {
        // Arrange
        var validIdentifier = "MyTable";

        // Act & Assert
        var exception = Record.Exception(() =>
            IdentifierValidator.ValidateOrThrow(validIdentifier, "table name"));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateOrThrow_NullOrWhiteSpace_ThrowsArgumentException(string? identifier)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            IdentifierValidator.ValidateOrThrow(identifier, "table name"));

        Assert.Contains("null or empty", exception.Message);
    }

    [Fact]
    public void ValidateOrThrow_InvalidIdentifier_ThrowsArgumentException()
    {
        // Arrange
        var invalidIdentifier = "Table-Name";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            IdentifierValidator.ValidateOrThrow(invalidIdentifier, "table name"));

        Assert.Contains("Invalid table name", exception.Message);
    }

    [Fact]
    public void ValidateOrThrow_SqlReservedKeyword_WithRestrictiveMode_ThrowsArgumentException()
    {
        // Arrange
        var keyword = "SELECT";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            IdentifierValidator.ValidateOrThrow(keyword, "table name", useRestrictiveMode: true));

        Assert.Contains("SQL reserved keyword", exception.Message);
    }

    [Fact]
    public void ValidateAllOrThrow_AllValidIdentifiers_DoesNotThrow()
    {
        // Arrange
        var identifiers = new[] { "Table1", "Table2", "Column_Name" };

        // Act & Assert
        var exception = Record.Exception(() =>
            IdentifierValidator.ValidateAllOrThrow(identifiers, "table names"));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateAllOrThrow_OneInvalidIdentifier_ThrowsArgumentException()
    {
        // Arrange
        var identifiers = new[] { "Table1", "Invalid-Table", "Table3" };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            IdentifierValidator.ValidateAllOrThrow(identifiers, "table names"));

        Assert.Contains("Invalid table names", exception.Message);
    }

    [Fact]
    public void FilterValid_MixedValidAndInvalid_ReturnsOnlyValid()
    {
        // Arrange
        var identifiers = new[]
        {
            "ValidTable1",
            "Invalid-Table",
            "ValidTable2",
            "Another Invalid",
            "_ValidUnderscore",
            "123Invalid"
        };

        // Act
        var validIdentifiers = IdentifierValidator.FilterValid(identifiers);

        // Assert
        Assert.Equal(3, validIdentifiers.Count);
        Assert.Contains("ValidTable1", validIdentifiers);
        Assert.Contains("ValidTable2", validIdentifiers);
        Assert.Contains("_ValidUnderscore", validIdentifiers);
    }

    [Fact]
    public void FilterValid_WithRestrictiveMode_FiltersOutLeadingUnderscores()
    {
        // Arrange
        var identifiers = new[]
        {
            "ValidTable",
            "_UnderscoreTable",
            "AnotherValid"
        };

        // Act
        var validIdentifiers = IdentifierValidator.FilterValid(identifiers, useRestrictiveMode: true);

        // Assert
        Assert.Equal(2, validIdentifiers.Count);
        Assert.Contains("ValidTable", validIdentifiers);
        Assert.Contains("AnotherValid", validIdentifiers);
        Assert.DoesNotContain("_UnderscoreTable", validIdentifiers);
    }

    [Fact]
    public void FilterValid_WithRestrictiveMode_FiltersOutSqlKeywords()
    {
        // Arrange
        var identifiers = new[]
        {
            "ValidTable",
            "SELECT",
            "DROP",
            "AnotherValid"
        };

        // Act
        var validIdentifiers = IdentifierValidator.FilterValid(identifiers, useRestrictiveMode: true);

        // Assert
        Assert.Equal(2, validIdentifiers.Count);
        Assert.Contains("ValidTable", validIdentifiers);
        Assert.Contains("AnotherValid", validIdentifiers);
        Assert.DoesNotContain("SELECT", validIdentifiers);
        Assert.DoesNotContain("DROP", validIdentifiers);
    }

    [Theory]
    [InlineData("'; DROP TABLE users--")]
    [InlineData("1' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("1; DELETE FROM users")]
    public void IsValid_SqlInjectionAttempts_ReturnsFalse(string injectionAttempt)
    {
        // Act
        var result = IdentifierValidator.IsValid(injectionAttempt);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValid_MaxLengthIdentifier_ReturnsTrue()
    {
        // Arrange - create identifier exactly 128 characters long
        var maxLengthIdentifier = new string('a', 128);

        // Act
        var result = IdentifierValidator.IsValid(maxLengthIdentifier);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("select")]
    [InlineData("Select")]
    [InlineData("SeLeCt")]
    public void IsValid_SqlKeywordsCaseInsensitive_WithRestrictiveMode_ReturnsFalse(string keyword)
    {
        // Act
        var result = IdentifierValidator.IsValid(keyword, useRestrictiveMode: true);

        // Assert - should be case-insensitive
        Assert.False(result);
    }
}
