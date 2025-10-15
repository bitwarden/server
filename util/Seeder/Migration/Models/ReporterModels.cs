namespace Bit.Seeder.Migration.Models;

public enum ImportStatus
{
    Success,
    Failed,
    Skipped,
    Partial
}

public enum VerificationStatus
{
    Verified,
    Mismatch,
    Missing,
    Error
}

public class TableImportStats
{
    public string TableName { get; set; } = string.Empty;
    public string DestinationTable { get; set; } = string.Empty;
    public ImportStatus Status { get; set; }
    public int RowsLoaded { get; set; }
    public int ExpectedRows { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Notes { get; set; }

    public TimeSpan Duration => EndTime - StartTime;

    public double RowsPerSecond
    {
        get
        {
            var seconds = Duration.TotalSeconds;
            return seconds > 0 ? RowsLoaded / seconds : 0;
        }
    }
}

public class TableVerificationStats
{
    public string TableName { get; set; } = string.Empty;
    public string DestinationTable { get; set; } = string.Empty;
    public VerificationStatus Status { get; set; }
    public int CsvRowCount { get; set; }
    public int DatabaseRowCount { get; set; }
    public string? ErrorMessage { get; set; }

    public int RowDifference => DatabaseRowCount - CsvRowCount;
}

public class ImportSummaryStats
{
    public int TotalTables { get; set; }
    public int SuccessfulTables { get; set; }
    public int FailedTables { get; set; }
    public int SkippedTables { get; set; }
    public int TotalRowsImported { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public TimeSpan TotalDuration => EndTime - StartTime;
    public int ErrorCount => FailedTables;
    public double SuccessRate => TotalTables > 0 ? (double)SuccessfulTables / TotalTables * 100 : 0;
}

public class VerificationSummaryStats
{
    public int TotalTables { get; set; }
    public int VerifiedTables { get; set; }
    public int MismatchedTables { get; set; }
    public int MissingTables { get; set; }
    public int ErrorTables { get; set; }

    public double SuccessRate => TotalTables > 0 ? (double)VerifiedTables / TotalTables * 100 : 0;
}

public enum ExportStatus
{
    Success,
    Failed,
    Skipped
}

public class TableExportStats
{
    public string TableName { get; set; } = string.Empty;
    public ExportStatus Status { get; set; }
    public int RowsExported { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Notes { get; set; }

    public TimeSpan Duration => EndTime - StartTime;

    public double RowsPerSecond
    {
        get
        {
            var seconds = Duration.TotalSeconds;
            return seconds > 0 ? RowsExported / seconds : 0;
        }
    }
}

public class ExportSummaryStats
{
    public int TotalTables { get; set; }
    public int SuccessfulTables { get; set; }
    public int FailedTables { get; set; }
    public int SkippedTables { get; set; }
    public int TotalRowsExported { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public TimeSpan TotalDuration => EndTime - StartTime;
    public int ErrorCount => FailedTables;
    public double SuccessRate => TotalTables > 0 ? (double)SuccessfulTables / TotalTables * 100 : 0;
}
