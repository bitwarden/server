namespace Bit.Core.Utilities;

/// <summary>
/// Provides cache key generation helpers and cache name constants for organization report–related entities.
/// </summary>
public static class OrganizationReportCacheConstants
{
    /// <summary>
    /// The cache name used for storing organization report data.
    /// </summary>
    public const string CacheName = "OrganizationReports";

    /// <summary>
    /// Duration TimeSpan for caching organization report summary data.
    /// Consider: Reports might be regenerated daily, so cache for shorter periods.
    /// </summary>
    public static readonly TimeSpan DurationForSummaryData = TimeSpan.FromHours(6);

    /// <summary>
    /// Builds a deterministic cache key for organization report summary data by date range.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="startDate">The start date of the date range.</param>
    /// <param name="endDate">The end date of the date range.</param>
    /// <returns>
    /// A cache key for the organization report summary data.
    /// </returns>
    public static string BuildCacheKeyForSummaryDataByDateRange(
        Guid organizationId,
        DateTime startDate,
        DateTime endDate)
        => $"OrganizationReportSummaryData:{organizationId:N}:{startDate:yyyy-MM-dd}:{endDate:yyyy-MM-dd}";

    /// <summary>
    /// Builds a cache tag for an organization's report data.
    /// Used for bulk invalidation when organization reports are updated.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <returns>
    /// A cache tag for the organization's reports.
    /// </returns>
    public static string BuildCacheTagForOrganizationReports(Guid organizationId)
        => $"OrganizationReports:{organizationId:N}";
}
