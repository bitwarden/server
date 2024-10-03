#nullable enable
namespace Bit.Api.NotificationCenter.Models.Request;

public class NotificationFilterRequestModel
{
    /// <summary>
    /// Filters notifications by read status. When not set, includes notifications without a status.
    /// </summary>
    public bool? ReadStatusFilter { get; set; }

    /// <summary>
    /// Filters notifications by deleted status. When not set, includes notifications without a status.
    /// </summary>
    public bool? DeletedStatusFilter { get; set; }

    /// <summary>
    /// A cursor for use in pagination.
    /// </summary>
    public string? ContinuationToken { get; set; }
}
