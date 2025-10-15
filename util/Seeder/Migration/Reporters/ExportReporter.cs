using Bit.Seeder.Migration.Models;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Migration.Reporters;

public class ExportReporter(ILogger<ExportReporter> logger)
{
    private readonly ILogger<ExportReporter> _logger = logger;
    private readonly List<TableExportStats> _tableStats = [];
    private DateTime _exportStartTime;
    private DateTime _exportEndTime;
    private TableExportStats? _currentTable;

    // ANSI color codes for console output
    private const string ColorGreen = "\x1b[32m";
    private const string ColorRed = "\x1b[31m";
    private const string ColorYellow = "\x1b[33m";
    private const string ColorBlue = "\x1b[34m";
    private const string ColorCyan = "\x1b[36m";
    private const string ColorBold = "\x1b[1m";
    private const string ColorReset = "\x1b[0m";

    // Separator constants for logging
    private const string Separator = "================================================================================";
    private const string ShortSeparator = "----------------------------------------";

    public void StartExport()
    {
        _exportStartTime = DateTime.Now;
        _tableStats.Clear();
        Console.WriteLine(Separator);
        Console.WriteLine($"{ColorBold}Starting Database Export{ColorReset}");
        Console.WriteLine(Separator);
    }

    public void StartTable(string tableName)
    {
        _currentTable = new TableExportStats
        {
            TableName = tableName,
            StartTime = DateTime.Now,
            Status = ExportStatus.Failed // Default to failed, will update on success
        };

        Console.WriteLine($"\n{ColorBlue}[TABLE]{ColorReset} {ColorBold}{tableName}{ColorReset}");
    }

    public void FinishTable(ExportStatus status, int rowsExported, string? errorMessage = null, string? notes = null)
    {
        if (_currentTable == null)
            return;

        _currentTable.EndTime = DateTime.Now;
        _currentTable.Status = status;
        _currentTable.RowsExported = rowsExported;
        _currentTable.ErrorMessage = errorMessage;
        _currentTable.Notes = notes;

        _tableStats.Add(_currentTable);

        // Log completion status
        var statusColor = status switch
        {
            ExportStatus.Success => ColorGreen,
            ExportStatus.Failed => ColorRed,
            ExportStatus.Skipped => ColorYellow,
            _ => ColorReset
        };

        var statusSymbol = status switch
        {
            ExportStatus.Success => "✓",
            ExportStatus.Failed => "✗",
            ExportStatus.Skipped => "⊘",
            _ => "?"
        };

        Console.WriteLine($"{statusColor}{statusSymbol} Status:{ColorReset} {status}");
        Console.WriteLine($"Rows exported: {rowsExported:N0}");
        Console.WriteLine($"Duration: {_currentTable.Duration.TotalSeconds:F2}s");
        Console.WriteLine($"Rate: {_currentTable.RowsPerSecond:F0} rows/sec");

        if (!string.IsNullOrEmpty(errorMessage))
        {
            Console.WriteLine($"{ColorRed}Error: {errorMessage}{ColorReset}");
        }

        if (!string.IsNullOrEmpty(notes))
        {
            Console.WriteLine($"Notes: {notes}");
        }

        _currentTable = null;
    }

    public void FinishExport()
    {
        _exportEndTime = DateTime.Now;
        PrintDetailedReport();
    }

    public ExportSummaryStats GetSummaryStats()
    {
        return new ExportSummaryStats
        {
            TotalTables = _tableStats.Count,
            SuccessfulTables = _tableStats.Count(t => t.Status == ExportStatus.Success),
            FailedTables = _tableStats.Count(t => t.Status == ExportStatus.Failed),
            SkippedTables = _tableStats.Count(t => t.Status == ExportStatus.Skipped),
            TotalRowsExported = _tableStats.Sum(t => t.RowsExported),
            StartTime = _exportStartTime,
            EndTime = _exportEndTime
        };
    }

    public List<TableExportStats> GetTableStats() => _tableStats.ToList();

    public void PrintDetailedReport()
    {
        var summary = GetSummaryStats();

        Console.WriteLine($"\n{Separator}");
        Console.WriteLine($"{ColorBold}Export Summary Report{ColorReset}");
        Console.WriteLine(Separator);

        // Overall statistics
        Console.WriteLine($"\n{ColorBold}Overall Statistics:{ColorReset}");
        Console.WriteLine($"  Total tables: {summary.TotalTables}");
        Console.WriteLine($"  {ColorGreen}✓ Successful:{ColorReset} {summary.SuccessfulTables}");

        if (summary.FailedTables > 0)
            Console.WriteLine($"  {ColorRed}✗ Failed:{ColorReset} {summary.FailedTables}");

        if (summary.SkippedTables > 0)
            Console.WriteLine($"  {ColorYellow}⊘ Skipped:{ColorReset} {summary.SkippedTables}");

        Console.WriteLine($"  Total rows exported: {summary.TotalRowsExported:N0}");
        Console.WriteLine($"  Total duration: {summary.TotalDuration.TotalMinutes:F2} minutes");
        Console.WriteLine($"  Success rate: {summary.SuccessRate:F1}%");

        // Per-table details
        if (_tableStats.Count > 0)
        {
            // Calculate dynamic column widths based on actual data
            var maxTableNameLength = _tableStats.Max(t => t.TableName.Length);
            var tableColumnWidth = Math.Max(30, maxTableNameLength + 2); // Minimum 30, add 2 for padding

            // Calculate max rows text length (format: "1,234,567")
            var maxRowsTextLength = _tableStats.Max(t => $"{t.RowsExported:N0}".Length);
            var rowsColumnWidth = Math.Max(15, maxRowsTextLength + 2); // Minimum 15, add 2 for padding

            // Calculate total width for dynamic separator
            // tableColumnWidth + space + 10 (status) + space + rowsColumnWidth + space + 12 (duration) + space + 10 (rate)
            var totalWidth = tableColumnWidth + 1 + 10 + 1 + rowsColumnWidth + 1 + 12 + 1 + 10;
            var dynamicSeparator = new string('=', totalWidth);

            Console.WriteLine($"\n{ColorBold}Per-Table Details:{ColorReset}");
            Console.WriteLine(dynamicSeparator);
            Console.WriteLine($"{"Table".PadRight(tableColumnWidth)} {"Status".PadRight(10)} {"Rows".PadRight(rowsColumnWidth)} {"Duration".PadRight(12)} {"Rate",10}");
            Console.WriteLine(dynamicSeparator);

            foreach (var stats in _tableStats.OrderBy(t => t.TableName))
            {
                var statusColor = stats.Status switch
                {
                    ExportStatus.Success => ColorGreen,
                    ExportStatus.Failed => ColorRed,
                    ExportStatus.Skipped => ColorYellow,
                    _ => ColorReset
                };

                var statusText = $"{statusColor}{stats.Status.ToString().PadRight(10)}{ColorReset}";
                var rowsText = $"{stats.RowsExported:N0}";
                var durationText = $"{stats.Duration.TotalSeconds:F1}s";
                var rateText = $"{stats.RowsPerSecond:F0}/s";

                Console.WriteLine($"{stats.TableName.PadRight(tableColumnWidth)} {statusText} {rowsText.PadRight(rowsColumnWidth)} {durationText.PadRight(12)} {rateText,10}");

                if (!string.IsNullOrEmpty(stats.ErrorMessage))
                {
                    Console.WriteLine($"  {ColorRed}→ {stats.ErrorMessage}{ColorReset}");
                }

                if (!string.IsNullOrEmpty(stats.Notes))
                {
                    Console.WriteLine($"  {ColorCyan}→ {stats.Notes}{ColorReset}");
                }
            }

            Console.WriteLine(dynamicSeparator);
        }

        // Failed tables summary
        var failedTables = _tableStats.Where(t => t.Status == ExportStatus.Failed).ToList();
        if (failedTables.Count > 0)
        {
            Console.WriteLine($"\n{ColorRed}{ColorBold}Failed Tables:{ColorReset}");
            foreach (var failed in failedTables)
            {
                Console.WriteLine($"  • {failed.TableName}: {failed.ErrorMessage}");
            }
        }

        // Performance insights
        if (_tableStats.Count > 0)
        {
            var successfulStats = _tableStats.Where(t => t.Status == ExportStatus.Success).ToList();
            var slowest = _tableStats.OrderByDescending(t => t.Duration).First();
            var fastest = _tableStats.Where(t => t.RowsExported > 0)
                                    .OrderByDescending(t => t.RowsPerSecond)
                                    .FirstOrDefault();

            Console.WriteLine($"\n{ColorBold}Performance Insights:{ColorReset}");

            if (successfulStats.Count > 0)
            {
                var avgRate = successfulStats.Average(t => t.RowsPerSecond);
                Console.WriteLine($"  Average export rate: {avgRate:F0} rows/sec");
            }

            Console.WriteLine($"  Slowest table: {slowest.TableName} ({slowest.Duration.TotalSeconds:F1}s)");
            if (fastest != null)
            {
                Console.WriteLine($"  Fastest table: {fastest.TableName} ({fastest.RowsPerSecond:F0} rows/sec)");
            }
        }

        Console.WriteLine($"\n{Separator}");

        // Final status
        if (summary.FailedTables == 0)
        {
            Console.WriteLine($"{ColorGreen}{ColorBold}✓ Export completed successfully!{ColorReset}");
        }
        else
        {
            Console.WriteLine($"{ColorRed}{ColorBold}✗ Export completed with {summary.FailedTables} failed table(s){ColorReset}");
        }

        Console.WriteLine($"{Separator}\n");
    }

    public void ExportReport(string filePath)
    {
        try
        {
            using var writer = new StreamWriter(filePath);
            var summary = GetSummaryStats();

            writer.WriteLine("Database Export Report");
            writer.WriteLine($"Generated: {DateTime.Now}");
            writer.WriteLine(new string('=', 80));
            writer.WriteLine();

            writer.WriteLine("Overall Statistics:");
            writer.WriteLine($"  Total tables: {summary.TotalTables}");
            writer.WriteLine($"  Successful: {summary.SuccessfulTables}");
            writer.WriteLine($"  Failed: {summary.FailedTables}");
            writer.WriteLine($"  Skipped: {summary.SkippedTables}");
            writer.WriteLine($"  Total rows exported: {summary.TotalRowsExported:N0}");
            writer.WriteLine($"  Total duration: {summary.TotalDuration.TotalMinutes:F2} minutes");
            writer.WriteLine($"  Success rate: {summary.SuccessRate:F1}%");
            writer.WriteLine();

            // Calculate dynamic column width based on longest table name
            var maxTableNameLength = _tableStats.Max(t => t.TableName.Length);
            var tableColumnWidth = Math.Max(30, maxTableNameLength + 2); // Minimum 30, add 2 for padding

            writer.WriteLine("Per-Table Details:");
            writer.WriteLine(new string('-', 80));
            writer.WriteLine($"{"Table".PadRight(tableColumnWidth)} {"Status",-10} {"Rows",-15} {"Duration",-12} {"Rate",10}");
            writer.WriteLine(new string('-', 80));

            foreach (var stats in _tableStats.OrderBy(t => t.TableName))
            {
                var rowsText = $"{stats.RowsExported:N0}";
                var durationText = $"{stats.Duration.TotalSeconds:F1}s";
                var rateText = $"{stats.RowsPerSecond:F0}/s";

                writer.WriteLine($"{stats.TableName.PadRight(tableColumnWidth)} {stats.Status,-10} {rowsText,-15} {durationText,-12} {rateText,10}");

                if (!string.IsNullOrEmpty(stats.ErrorMessage))
                {
                    writer.WriteLine($"  Error: {stats.ErrorMessage}");
                }

                if (!string.IsNullOrEmpty(stats.Notes))
                {
                    writer.WriteLine($"  Notes: {stats.Notes}");
                }
            }

            writer.WriteLine(new string('-', 80));

            _logger.LogInformation("Export report exported to: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to export report: {Message}", ex.Message);
        }
    }
}
