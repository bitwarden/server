namespace Bit.Seeder.Migration.Utils;

/// <summary>
/// Constants used throughout the DbSeeder utility for database operations.
/// </summary>
public static class DbSeederConstants
{
    /// <summary>
    /// Default sample size for type detection and data analysis.
    /// </summary>
    public const int DEFAULT_SAMPLE_SIZE = 100;

    /// <summary>
    /// Default batch size for bulk insert operations.
    /// </summary>
    public const int DEFAULT_BATCH_SIZE = 1000;

    /// <summary>
    /// Large batch size for optimized bulk operations (use with caution for max_allowed_packet limits).
    /// </summary>
    public const int LARGE_BATCH_SIZE = 5000;

    /// <summary>
    /// Default connection timeout in seconds.
    /// </summary>
    public const int DEFAULT_CONNECTION_TIMEOUT = 30;

    /// <summary>
    /// Default command timeout in seconds for regular operations.
    /// </summary>
    public const int DEFAULT_COMMAND_TIMEOUT = 60;

    /// <summary>
    /// Extended command timeout in seconds for large batch operations.
    /// </summary>
    public const int LARGE_BATCH_COMMAND_TIMEOUT = 300; // 5 minutes

    /// <summary>
    /// Default maximum pool size for database connections.
    /// </summary>
    public const int DEFAULT_MAX_POOL_SIZE = 100;

    /// <summary>
    /// Threshold for enabling detailed progress logging (row count).
    /// Operations with fewer rows may use simpler logging.
    /// </summary>
    public const int LOGGING_THRESHOLD = 1000;

    /// <summary>
    /// Batch size for progress reporting during long-running operations.
    /// </summary>
    public const int PROGRESS_REPORTING_INTERVAL = 10000;

    /// <summary>
    /// Placeholder text for redacting passwords in connection strings for safe logging.
    /// </summary>
    public const string REDACTED_PASSWORD = "***REDACTED***";
}
