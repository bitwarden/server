using Bit.Seeder.Migration.Models;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Migration.Reporters;

public class VerificationReporter(ILogger<VerificationReporter> logger)
{
    private readonly ILogger<VerificationReporter> _logger = logger;
    private readonly List<TableVerificationStats> _tableStats = [];

    // ANSI color codes for console output
    private const string ColorGreen = "\x1b[32m";
    private const string ColorRed = "\x1b[31m";
    private const string ColorYellow = "\x1b[33m";
    private const string ColorBlue = "\x1b[34m";
    private const string ColorBold = "\x1b[1m";
    private const string ColorReset = "\x1b[0m";

    // Separator constants for logging
    private const string Separator = "================================================================================";
    private const string ShortSeparator = "----------------------------------------";

    public void StartVerification()
    {
        _tableStats.Clear();
        Console.WriteLine(Separator);
        Console.WriteLine($"{ColorBold}Starting Import Verification{ColorReset}");
        Console.WriteLine(Separator);
    }

    public void VerifyTable(
        string tableName,
        string destinationTable,
        int csvRowCount,
        int databaseRowCount,
        string? errorMessage = null)
    {
        var status = DetermineStatus(csvRowCount, databaseRowCount, errorMessage);

        var stats = new TableVerificationStats
        {
            TableName = tableName,
            DestinationTable = destinationTable,
            CsvRowCount = csvRowCount,
            DatabaseRowCount = databaseRowCount,
            Status = status,
            ErrorMessage = errorMessage
        };

        _tableStats.Add(stats);

        // Log verification result
        var statusColor = status switch
        {
            VerificationStatus.Verified => ColorGreen,
            VerificationStatus.Mismatch => ColorRed,
            VerificationStatus.Missing => ColorYellow,
            VerificationStatus.Error => ColorRed,
            _ => ColorReset
        };

        var statusSymbol = status switch
        {
            VerificationStatus.Verified => "✓",
            VerificationStatus.Mismatch => "✗",
            VerificationStatus.Missing => "?",
            VerificationStatus.Error => "!",
            _ => "?"
        };

        Console.WriteLine($"\n{ColorBlue}[TABLE]{ColorReset} {ColorBold}{tableName}{ColorReset} -> {destinationTable}");
        Console.WriteLine($"{statusColor}{statusSymbol} Status:{ColorReset} {status}");
        Console.WriteLine($"CSV rows: {csvRowCount:N0}");
        Console.WriteLine($"Database rows: {databaseRowCount:N0}");

        if (stats.RowDifference != 0)
        {
            var diffColor = stats.RowDifference > 0 ? ColorGreen : ColorRed;
            Console.WriteLine($"Difference: {diffColor}{stats.RowDifference:+#;-#;0}{ColorReset}");
        }

        if (!string.IsNullOrEmpty(errorMessage))
        {
            Console.WriteLine($"{ColorRed}Error: {errorMessage}{ColorReset}");
        }
    }

    public void FinishVerification()
    {
        PrintVerificationReport();
    }

    public VerificationSummaryStats GetSummaryStats()
    {
        return new VerificationSummaryStats
        {
            TotalTables = _tableStats.Count,
            VerifiedTables = _tableStats.Count(t => t.Status == VerificationStatus.Verified),
            MismatchedTables = _tableStats.Count(t => t.Status == VerificationStatus.Mismatch),
            MissingTables = _tableStats.Count(t => t.Status == VerificationStatus.Missing),
            ErrorTables = _tableStats.Count(t => t.Status == VerificationStatus.Error)
        };
    }

    public List<TableVerificationStats> GetTableStats() => _tableStats.ToList();

    public void PrintVerificationReport()
    {
        var summary = GetSummaryStats();

        Console.WriteLine($"\n{Separator}");
        Console.WriteLine($"{ColorBold}Verification Summary Report{ColorReset}");
        Console.WriteLine(Separator);

        // Overall statistics
        Console.WriteLine($"\n{ColorBold}Overall Statistics:{ColorReset}");
        Console.WriteLine($"  Total tables: {summary.TotalTables}");
        Console.WriteLine($"  {ColorGreen}✓ Verified:{ColorReset} {summary.VerifiedTables}");

        if (summary.MismatchedTables > 0)
            Console.WriteLine($"  {ColorRed}✗ Mismatched:{ColorReset} {summary.MismatchedTables}");

        if (summary.MissingTables > 0)
            Console.WriteLine($"  {ColorYellow}? Missing:{ColorReset} {summary.MissingTables}");

        if (summary.ErrorTables > 0)
            Console.WriteLine($"  {ColorRed}! Errors:{ColorReset} {summary.ErrorTables}");

        Console.WriteLine($"  Success rate: {summary.SuccessRate:F1}%");

        // Per-table details
        if (_tableStats.Count > 0)
        {
            // Calculate dynamic column widths based on actual data
            var maxTableNameLength = _tableStats.Max(t => t.TableName.Length);
            var tableColumnWidth = Math.Max(30, maxTableNameLength + 2); // Minimum 30, add 2 for padding

            // Calculate max text lengths for numeric columns
            var maxCsvTextLength = _tableStats.Max(t => $"{t.CsvRowCount:N0}".Length);
            var maxDbTextLength = _tableStats.Max(t => $"{t.DatabaseRowCount:N0}".Length);
            var csvColumnWidth = Math.Max(10, maxCsvTextLength + 2); // Minimum 10, add 2 for padding
            var dbColumnWidth = Math.Max(10, maxDbTextLength + 2); // Minimum 10, add 2 for padding

            // Calculate total width for dynamic separator
            // tableColumnWidth + space + 12 (status) + space + csvColumnWidth + space + dbColumnWidth + space + 10 (diff)
            var totalWidth = tableColumnWidth + 1 + 12 + 1 + csvColumnWidth + 1 + dbColumnWidth + 1 + 10;
            var dynamicSeparator = new string('=', totalWidth);

            Console.WriteLine($"\n{ColorBold}Per-Table Details:{ColorReset}");
            Console.WriteLine(dynamicSeparator);
            Console.WriteLine($"{"Table".PadRight(tableColumnWidth)} {"Status".PadRight(12)} {"CSV Rows".PadLeft(csvColumnWidth)} {"DB Rows".PadLeft(dbColumnWidth)} {"Diff",10}");
            Console.WriteLine(dynamicSeparator);

            foreach (var stats in _tableStats.OrderBy(t => t.TableName))
            {
                var statusColor = stats.Status switch
                {
                    VerificationStatus.Verified => ColorGreen,
                    VerificationStatus.Mismatch => ColorRed,
                    VerificationStatus.Missing => ColorYellow,
                    VerificationStatus.Error => ColorRed,
                    _ => ColorReset
                };

                var statusText = $"{statusColor}{stats.Status.ToString().PadRight(12)}{ColorReset}";
                var csvText = $"{stats.CsvRowCount:N0}";
                var dbText = $"{stats.DatabaseRowCount:N0}";
                var diffText = stats.RowDifference != 0
                    ? $"{(stats.RowDifference > 0 ? ColorGreen : ColorRed)}{stats.RowDifference:+#;-#;0}{ColorReset}"
                    : "0";

                Console.WriteLine($"{stats.TableName.PadRight(tableColumnWidth)} {statusText} {csvText.PadLeft(csvColumnWidth)} {dbText.PadLeft(dbColumnWidth)} {diffText,10}");

                if (!string.IsNullOrEmpty(stats.ErrorMessage))
                {
                    Console.WriteLine($"  {ColorRed}→ {stats.ErrorMessage}{ColorReset}");
                }
            }

            Console.WriteLine(dynamicSeparator);
        }

        // Problem tables
        var problemTables = _tableStats
            .Where(t => t.Status != VerificationStatus.Verified)
            .ToList();

        if (problemTables.Count > 0)
        {
            Console.WriteLine($"\n{ColorRed}{ColorBold}Tables Needing Attention:{ColorReset}");

            foreach (var problem in problemTables)
            {
                var issueType = problem.Status switch
                {
                    VerificationStatus.Mismatch => "Row count mismatch",
                    VerificationStatus.Missing => "CSV file not found",
                    VerificationStatus.Error => "Verification error",
                    _ => "Unknown issue"
                };

                Console.WriteLine($"  • {problem.TableName}: {issueType}");

                if (problem.Status == VerificationStatus.Mismatch)
                {
                    Console.WriteLine($"    Expected: {problem.CsvRowCount:N0}, Found: {problem.DatabaseRowCount:N0}");
                }

                if (!string.IsNullOrEmpty(problem.ErrorMessage))
                {
                    Console.WriteLine($"    Error: {problem.ErrorMessage}");
                }
            }
        }

        Console.WriteLine($"\n{Separator}");

        // Final status
        if (summary.MismatchedTables == 0 && summary.ErrorTables == 0 && summary.MissingTables == 0)
        {
            Console.WriteLine($"{ColorGreen}{ColorBold}✓ All tables verified successfully!{ColorReset}");
        }
        else
        {
            var problemCount = summary.MismatchedTables + summary.ErrorTables + summary.MissingTables;
            Console.WriteLine($"{ColorRed}{ColorBold}✗ Verification completed with {problemCount} issue(s){ColorReset}");
        }

        Console.WriteLine($"{Separator}\n");
    }

    public void ExportReport(string filePath)
    {
        try
        {
            using var writer = new StreamWriter(filePath);
            var summary = GetSummaryStats();

            writer.WriteLine("Database Verification Report");
            writer.WriteLine($"Generated: {DateTime.Now}");
            writer.WriteLine(new string('=', 80));
            writer.WriteLine();

            writer.WriteLine("Overall Statistics:");
            writer.WriteLine($"  Total tables: {summary.TotalTables}");
            writer.WriteLine($"  Verified: {summary.VerifiedTables}");
            writer.WriteLine($"  Mismatched: {summary.MismatchedTables}");
            writer.WriteLine($"  Missing: {summary.MissingTables}");
            writer.WriteLine($"  Errors: {summary.ErrorTables}");
            writer.WriteLine($"  Success rate: {summary.SuccessRate:F1}%");
            writer.WriteLine();

            // Calculate dynamic column width based on longest table name
            var maxTableNameLength = _tableStats.Max(t => t.TableName.Length);
            var tableColumnWidth = Math.Max(30, maxTableNameLength + 2); // Minimum 30, add 2 for padding

            writer.WriteLine("Per-Table Details:");
            writer.WriteLine(new string('-', 80));
            writer.WriteLine($"{"Table".PadRight(tableColumnWidth)} {"Status",-12} {"CSV Rows",12} {"DB Rows",12} {"Diff",10}");
            writer.WriteLine(new string('-', 80));

            foreach (var stats in _tableStats.OrderBy(t => t.TableName))
            {
                var csvText = $"{stats.CsvRowCount:N0}";
                var dbText = $"{stats.DatabaseRowCount:N0}";
                var diffText = stats.RowDifference != 0 ? $"{stats.RowDifference:+#;-#;0}" : "0";

                writer.WriteLine($"{stats.TableName.PadRight(tableColumnWidth)} {stats.Status,-12} {csvText,12} {dbText,12} {diffText,10}");

                if (!string.IsNullOrEmpty(stats.ErrorMessage))
                {
                    writer.WriteLine($"  Error: {stats.ErrorMessage}");
                }
            }

            writer.WriteLine(new string('-', 80));

            _logger.LogInformation("Verification report exported to: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to export report: {Message}", ex.Message);
        }
    }

    private static VerificationStatus DetermineStatus(int csvRowCount, int databaseRowCount, string? errorMessage)
    {
        if (!string.IsNullOrEmpty(errorMessage))
            return VerificationStatus.Error;

        if (csvRowCount < 0)
            return VerificationStatus.Missing;

        if (csvRowCount == databaseRowCount)
            return VerificationStatus.Verified;

        return VerificationStatus.Mismatch;
    }
}
