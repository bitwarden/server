using System.Globalization;
using System.Text;
using Bit.Seeder.Migration.Models;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Bit.Seeder.Migration;

public class CsvHandler(CsvSettings settings, ILogger<CsvHandler> logger)
{
    private readonly ILogger<CsvHandler> _logger = logger;
    private readonly CsvSettings _settings = settings;
    private readonly string _outputDir = settings.OutputDir;
    private readonly string _delimiter = settings.Delimiter;
    private readonly string _fallbackDelimiter = settings.FallbackDelimiter;
    private readonly Encoding _encoding = new UTF8Encoding(false);

    public string ExportTableToCsv(
        string tableName,
        List<string> columns,
        List<object[]> data,
        List<string>? specialColumns = null)
    {
        specialColumns ??= [];
        var csvPath = Path.Combine(_outputDir, $"{tableName}.csv");

        _logger.LogInformation("Exporting {TableName} to {CsvPath}", tableName, csvPath);
        _logger.LogInformation("Special JSON columns: {Columns}", string.Join(", ", specialColumns));

        try
        {
            // Ensure output directory exists
            Directory.CreateDirectory(_outputDir);

            // Test if we can write with primary delimiter
            var delimiterToUse = TestDelimiterCompatibility(data, columns, specialColumns);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = delimiterToUse,
                HasHeaderRecord = _settings.IncludeHeaders,
                Encoding = _encoding,
                ShouldQuote = _ => true  // Always quote all fields (QUOTE_ALL)
            };

            using var writer = new StreamWriter(csvPath, false, _encoding);
            using var csv = new CsvWriter(writer, config);

            // Write headers if requested
            if (_settings.IncludeHeaders)
            {
                foreach (var column in columns)
                {
                    csv.WriteField(column);
                }
                csv.NextRecord();
            }

            // Write data rows
            var rowsWritten = 0;
            foreach (var row in data)
            {
                var processedRow = ProcessRowForExport(row, columns, specialColumns);
                foreach (var field in processedRow)
                {
                    csv.WriteField(field);
                }
                csv.NextRecord();
                rowsWritten++;
            }

            _logger.LogInformation("Successfully exported {RowsWritten} rows to {CsvPath}", rowsWritten, csvPath);
            return csvPath;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error exporting table {TableName}: {Message}", tableName, ex.Message);
            throw;
        }
    }

    public (List<string> Columns, List<object[]> Data) ImportCsvToData(
        string csvPath,
        List<string>? specialColumns = null)
    {
        specialColumns ??= [];

        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException($"CSV file not found: {csvPath}");
        }

        _logger.LogInformation("Reading data from {CsvPath}", csvPath);

        try
        {
            // Detect delimiter
            var delimiterUsed = DetectCsvDelimiter(csvPath);
            _logger.LogDebug("Detected delimiter for {CsvPath}: '{Delimiter}' (ASCII: {Ascii})", csvPath, delimiterUsed, (int)delimiterUsed[0]);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = delimiterUsed,
                HasHeaderRecord = _settings.IncludeHeaders,
                Encoding = _encoding,
                BadDataFound = null, // Ignore bad data
                TrimOptions = CsvHelper.Configuration.TrimOptions.None // Don't trim anything
            };

            using var reader = new StreamReader(csvPath, _encoding);
            using var csv = new CsvReader(reader, config);

            var columns = new List<string>();
            var dataRows = new List<object[]>();

            // Read headers if present
            if (_settings.IncludeHeaders)
            {
                csv.Read();
                csv.ReadHeader();
                var rawColumns = csv.HeaderRecord?.ToList() ?? [];
                _logger.LogDebug("Raw columns from CSV: {Columns}", string.Join(", ", rawColumns));
                // Remove surrounding quotes from column names if present
                columns = rawColumns.Select(col => col.Trim('"')).ToList();
                _logger.LogDebug("Cleaned columns: {Columns}", string.Join(", ", columns));
            }

            // Read data rows
            while (csv.Read())
            {
                var row = new List<object>();
                for (int i = 0; i < columns.Count; i++)
                {
                    var field = csv.GetField(i) ?? string.Empty;
                    row.Add(field);
                }
                var processedRow = ProcessRowForImport(row.ToArray(), columns, specialColumns);
                dataRows.Add(processedRow);
            }

            _logger.LogInformation("Successfully read {RowCount} rows from {CsvPath}", dataRows.Count, csvPath);
            return (columns, dataRows);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error importing CSV {CsvPath}: {Message}", csvPath, ex.Message);
            throw;
        }
    }

    private string TestDelimiterCompatibility(
        List<object[]> data,
        List<string> columns,
        List<string> specialColumns)
    {
        // Check a sample of rows for delimiter conflicts
        var sampleSize = Math.Min(100, data.Count);
        var specialColIndices = columns
            .Select((col, idx) => new { col, idx })
            .Where(x => specialColumns.Contains(x.col))
            .Select(x => x.idx)
            .ToList();

        foreach (var row in data.Take(sampleSize))
        {
            foreach (var colIdx in specialColIndices)
            {
                if (colIdx < row.Length && row[colIdx] != null)
                {
                    var cellValue = row[colIdx]?.ToString() ?? string.Empty;
                    // If primary delimiter appears in JSON data, use fallback
                    if (cellValue.Contains(_delimiter) && !IsProperlyQuoted(cellValue))
                    {
                        _logger.LogInformation(
                            "Primary delimiter '{Delimiter}' found in data, using fallback '{FallbackDelimiter}'", _delimiter, _fallbackDelimiter);
                        return _fallbackDelimiter;
                    }
                }
            }
        }

        return _delimiter;
    }

    private bool IsProperlyQuoted(string value)
    {
        return value.StartsWith("\"") && value.EndsWith("\"");
    }

    private string DetectCsvDelimiter(string csvPath)
    {
        using var reader = new StreamReader(csvPath, _encoding);
        // Read just the first line (header) for delimiter detection
        var firstLine = reader.ReadLine();
        if (string.IsNullOrEmpty(firstLine))
            return ",";

        // Count delimiters outside of quoted fields
        var commaCount = CountDelimitersOutsideQuotes(firstLine, ',');
        var pipeCount = CountDelimitersOutsideQuotes(firstLine, '|');
        var tabCount = CountDelimitersOutsideQuotes(firstLine, '\t');

        _logger.LogDebug("Delimiter counts - comma: {CommaCount}, pipe: {PipeCount}, tab: {TabCount}", commaCount, pipeCount, tabCount);

        if (pipeCount > commaCount && pipeCount > tabCount)
            return "|";
        if (tabCount > commaCount && tabCount > pipeCount)
            return "\t";

        return ",";
    }

    private int CountDelimitersOutsideQuotes(string line, char delimiter)
    {
        int count = 0;
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                // Handle escaped quotes (double quotes)
                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    i++; // Skip the next quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (line[i] == delimiter && !inQuotes)
            {
                count++;
            }
        }

        return count;
    }

    private object[] ProcessRowForExport(
        object[] row,
        List<string> columns,
        List<string> specialColumns)
    {
        var processedRow = new object[row.Length];

        for (int i = 0; i < row.Length; i++)
        {
            var colName = i < columns.Count ? columns[i] : $"col_{i}";

            if (row[i] == null)
            {
                processedRow[i] = string.Empty;
            }
            else if (specialColumns.Contains(colName))
            {
                // Handle JSON/encrypted data
                processedRow[i] = PrepareJsonForCsv(row[i]);
            }
            else if (row[i] is DateTime dt)
            {
                // Format DateTime with full precision (microseconds)
                // Format: yyyy-MM-dd HH:mm:ss.ffffff to match Python output
                processedRow[i] = dt.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
            }
            else
            {
                // Handle regular data
                processedRow[i] = row[i].ToString() ?? string.Empty;
            }
        }

        return processedRow;
    }

    private object[] ProcessRowForImport(
        object[] row,
        List<string> columns,
        List<string> specialColumns)
    {
        var processedRow = new object[row.Length];

        for (int i = 0; i < row.Length; i++)
        {
            var colName = i < columns.Count ? columns[i] : $"col_{i}";
            var value = row[i]?.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(value))
            {
                processedRow[i] = DBNull.Value;
            }
            else if (specialColumns.Contains(colName))
            {
                // Handle JSON/encrypted data
                processedRow[i] = RestoreJsonFromCsv(value) ?? value;
            }
            else
            {
                // Handle regular data
                processedRow[i] = value;
            }
        }

        return processedRow;
    }

    private string PrepareJsonForCsv(object jsonData)
    {
        if (jsonData == null)
            return string.Empty;

        var jsonStr = jsonData.ToString() ?? string.Empty;

        // Validate if it's valid JSON (for logging purposes)
        try
        {
            JsonConvert.DeserializeObject(jsonStr);
            _logger.LogDebug("Valid JSON data prepared for CSV export");
        }
        catch (JsonException)
        {
            _logger.LogDebug("Non-JSON string data prepared for CSV export");
        }

        // Let CSV writer handle the escaping
        return jsonStr;
    }

    private string? RestoreJsonFromCsv(string csvData)
    {
        if (string.IsNullOrEmpty(csvData))
            return null;

        // Return as-is - the CSV reader should have handled unescaping
        return csvData;
    }

    public bool ValidateExport(int originalCount, string csvPath)
    {
        try
        {
            using var reader = new StreamReader(csvPath, _encoding);
            var rowCount = 0L;
            while (reader.ReadLine() != null)
            {
                rowCount++;
            }

            // Subtract header row if present
            if (_settings.IncludeHeaders)
            {
                rowCount--;
            }

            if ((int)rowCount == originalCount)
            {
                _logger.LogInformation("Export validation passed: {RowCount} rows", rowCount);
                return true;
            }
            else
            {
                _logger.LogError("Export validation failed: expected {Expected}, got {Actual}", originalCount, rowCount);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error validating export: {Message}", ex.Message);
            return false;
        }
    }
}
