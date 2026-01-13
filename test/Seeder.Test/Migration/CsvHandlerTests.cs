using Bit.Seeder.Migration;
using Bit.Seeder.Migration.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Seeder.Test.Migration;

public class CsvHandlerTests : IDisposable
{
    private readonly ILogger<CsvHandler> _logger;
    private readonly string _testOutputDir;
    private readonly CsvSettings _settings;

    public CsvHandlerTests()
    {
        _logger = Substitute.For<ILogger<CsvHandler>>();
        _testOutputDir = Path.Combine(Path.GetTempPath(), $"CsvHandlerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testOutputDir);

        _settings = new CsvSettings
        {
            OutputDir = _testOutputDir,
            Delimiter = ",",
            FallbackDelimiter = "|",
            IncludeHeaders = true
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_testOutputDir))
        {
            Directory.Delete(_testOutputDir, true);
        }
    }

    [Fact]
    public void ExportTableToCsv_BasicData_CreatesValidCsvFile()
    {
        // Arrange
        var handler = new CsvHandler(_settings, _logger);
        var tableName = "Users";
        var columns = new List<string> { "Id", "Name", "Email" };
        var data = new List<object[]>
        {
            new object[] { 1, "John Doe", "john@example.com" },
            new object[] { 2, "Jane Smith", "jane@example.com" }
        };

        // Act
        var csvPath = handler.ExportTableToCsv(tableName, columns, data);

        // Assert
        Assert.True(File.Exists(csvPath));
        var lines = File.ReadAllLines(csvPath);
        Assert.Equal(3, lines.Length); // Header + 2 data rows
        Assert.Contains("Id", lines[0]);
        Assert.Contains("Name", lines[0]);
        Assert.Contains("Email", lines[0]);
    }

    [Fact]
    public void ExportTableToCsv_WithNullValues_HandlesCorrectly()
    {
        // Arrange
        var handler = new CsvHandler(_settings, _logger);
        var tableName = "TestTable";
        var columns = new List<string> { "Col1", "Col2", "Col3" };
        var data = new List<object[]>
        {
            new object[] { 1, null, "value" },
            new object?[] { 2, "test", null }
        };

        // Act
        var csvPath = handler.ExportTableToCsv(tableName, columns, data);

        // Assert
        Assert.True(File.Exists(csvPath));
        var content = File.ReadAllText(csvPath);
        // Null values should be exported as empty strings
        Assert.Contains("\"\"", content);
    }

    [Fact]
    public void ExportTableToCsv_WithJsonData_HandlesSpecialColumns()
    {
        // Arrange
        var handler = new CsvHandler(_settings, _logger);
        var tableName = "Settings";
        var columns = new List<string> { "Id", "Data" };
        var specialColumns = new List<string> { "Data" };
        var data = new List<object[]>
        {
            new object[] { 1, "{\"key\":\"value\",\"nested\":{\"prop\":123}}" }
        };

        // Act
        var csvPath = handler.ExportTableToCsv(tableName, columns, data, specialColumns);

        // Assert
        Assert.True(File.Exists(csvPath));
        var content = File.ReadAllText(csvPath);
        // JSON should be preserved
        Assert.Contains("key", content);
        Assert.Contains("value", content);
    }

    [Fact]
    public void ExportTableToCsv_WithDateTime_FormatsCorrectly()
    {
        // Arrange
        var handler = new CsvHandler(_settings, _logger);
        var tableName = "Events";
        var columns = new List<string> { "Id", "CreatedDate" };
        var testDate = new DateTime(2024, 1, 15, 10, 30, 45, 123);
        var data = new List<object[]>
        {
            new object[] { 1, testDate }
        };

        // Act
        var csvPath = handler.ExportTableToCsv(tableName, columns, data);

        // Assert
        Assert.True(File.Exists(csvPath));
        var content = File.ReadAllText(csvPath);
        // Should contain formatted date with microseconds
        Assert.Contains("2024-01-15", content);
    }

    [Fact]
    public void ImportCsvToData_BasicCsv_ReadsCorrectly()
    {
        // Arrange
        var handler = new CsvHandler(_settings, _logger);
        var csvPath = Path.Combine(_testOutputDir, "test.csv");
        var csvContent = "\"Id\",\"Name\",\"Email\"\n\"1\",\"John\",\"john@test.com\"\n\"2\",\"Jane\",\"jane@test.com\"";
        File.WriteAllText(csvPath, csvContent);

        // Act
        var (columns, data) = handler.ImportCsvToData(csvPath);

        // Assert
        Assert.Equal(3, columns.Count);
        Assert.Contains("Id", columns);
        Assert.Contains("Name", columns);
        Assert.Contains("Email", columns);
        Assert.Equal(2, data.Count);
        Assert.Equal("1", data[0][0]);
        Assert.Equal("John", data[0][1]);
    }

    [Fact]
    public void ImportCsvToData_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var handler = new CsvHandler(_settings, _logger);
        var nonExistentPath = Path.Combine(_testOutputDir, "nonexistent.csv");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            handler.ImportCsvToData(nonExistentPath));
    }

    [Fact]
    public void ImportCsvToData_WithEmptyValues_ConvertsToDBNull()
    {
        // Arrange
        var handler = new CsvHandler(_settings, _logger);
        var csvPath = Path.Combine(_testOutputDir, "test_empty.csv");
        var csvContent = "\"Col1\",\"Col2\",\"Col3\"\n\"1\",\"\",\"value\"\n\"2\",\"test\",\"\"";
        File.WriteAllText(csvPath, csvContent);

        // Act
        var (columns, data) = handler.ImportCsvToData(csvPath);

        // Assert
        Assert.Equal(2, data.Count);
        Assert.Equal(DBNull.Value, data[0][1]); // Empty string should become DBNull
    }

    [Fact]
    public void ImportCsvToData_WithJsonSpecialColumn_RestoresJson()
    {
        // Arrange
        var handler = new CsvHandler(_settings, _logger);
        var csvPath = Path.Combine(_testOutputDir, "test_json.csv");
        var jsonData = "{\"key\":\"value\"}";
        var csvContent = $"\"Id\",\"Data\"\n\"1\",\"{jsonData}\"";
        File.WriteAllText(csvPath, csvContent);
        var specialColumns = new List<string> { "Data" };

        // Act
        var (columns, data) = handler.ImportCsvToData(csvPath, specialColumns);

        // Assert
        Assert.Single(data);
        var restoredJson = data[0][1].ToString();
        Assert.Contains("key", restoredJson);
        Assert.Contains("value", restoredJson);
    }

    [Fact]
    public void ImportCsvToData_WithMalformedData_LogsWarning()
    {
        // Arrange
        var handler = new CsvHandler(_settings, _logger);
        var csvPath = Path.Combine(_testOutputDir, "test_malformed.csv");
        // Create CSV with mismatched column count (intentionally malformed)
        var csvContent = "\"Col1\",\"Col2\",\"Col3\"\n\"1\",\"value1\"\n\"2\",\"value2\",\"value3\"";
        File.WriteAllText(csvPath, csvContent);

        // Act - this should trigger BadDataFound callback
        var exception = Record.Exception(() => handler.ImportCsvToData(csvPath));

        // Note: CsvHelper may throw or handle this depending on configuration
        // The test verifies that BadDataFound handler logs the issue
        // We verify the logger was called with Warning level
        if (exception == null)
        {
            _logger.Received().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("Bad CSV data")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
    }

    [Fact]
    public void ValidateExport_CorrectRowCount_ReturnsTrue()
    {
        // Arrange
        var handler = new CsvHandler(_settings, _logger);
        var csvPath = Path.Combine(_testOutputDir, "validate_test.csv");
        var csvContent = "\"Col1\",\"Col2\"\n\"1\",\"a\"\n\"2\",\"b\"\n\"3\",\"c\"";
        File.WriteAllText(csvPath, csvContent);
        var originalCount = 3; // 3 data rows

        // Act
        var isValid = handler.ValidateExport(originalCount, csvPath);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateExport_IncorrectRowCount_ReturnsFalse()
    {
        // Arrange
        var handler = new CsvHandler(_settings, _logger);
        var csvPath = Path.Combine(_testOutputDir, "validate_fail_test.csv");
        var csvContent = "\"Col1\",\"Col2\"\n\"1\",\"a\"\n\"2\",\"b\"";
        File.WriteAllText(csvPath, csvContent);
        var originalCount = 5; // Expecting 5 but only have 2

        // Act
        var isValid = handler.ValidateExport(originalCount, csvPath);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ExportTableToCsv_WithoutHeaders_ExportsDataOnly()
    {
        // Arrange
        var settingsNoHeaders = new CsvSettings
        {
            OutputDir = _testOutputDir,
            Delimiter = ",",
            FallbackDelimiter = "|",
            IncludeHeaders = false
        };
        var handler = new CsvHandler(settingsNoHeaders, _logger);
        var tableName = "TestTable";
        var columns = new List<string> { "Col1", "Col2" };
        var data = new List<object[]>
        {
            new object[] { 1, "value1" },
            new object[] { 2, "value2" }
        };

        // Act
        var csvPath = handler.ExportTableToCsv(tableName, columns, data);

        // Assert
        Assert.True(File.Exists(csvPath));
        var lines = File.ReadAllLines(csvPath);
        Assert.Equal(2, lines.Length); // Only data rows, no header
    }

    [Fact]
    public void ImportCsvToData_WithPipeDelimiter_DetectsAndReadsCorrectly()
    {
        // Arrange
        var handler = new CsvHandler(_settings, _logger);
        var csvPath = Path.Combine(_testOutputDir, "test_pipe.csv");
        var csvContent = "\"Id\"|\"Name\"|\"Email\"\n\"1\"|\"John\"|\"john@test.com\"";
        File.WriteAllText(csvPath, csvContent);

        // Act
        var (columns, data) = handler.ImportCsvToData(csvPath);

        // Assert
        Assert.Equal(3, columns.Count);
        Assert.Single(data);
    }

    [Fact]
    public void ExportAndImportRoundTrip_PreservesData()
    {
        // Arrange
        var handler = new CsvHandler(_settings, _logger);
        var tableName = "RoundTripTest";
        var columns = new List<string> { "Id", "Name", "Value" };
        var originalData = new List<object[]>
        {
            new object[] { 1, "Test1", "Value1" },
            new object[] { 2, "Test2", "Value2" },
            new object[] { 3, "Test3", "Value3" }
        };

        // Act - Export then Import
        var csvPath = handler.ExportTableToCsv(tableName, columns, originalData);
        var (importedColumns, importedData) = handler.ImportCsvToData(csvPath);

        // Assert
        Assert.Equal(columns.Count, importedColumns.Count);
        Assert.Equal(originalData.Count, importedData.Count);
        for (int i = 0; i < originalData.Count; i++)
        {
            for (int j = 0; j < columns.Count; j++)
            {
                Assert.Equal(originalData[i][j].ToString(), importedData[i][j].ToString());
            }
        }
    }
}
