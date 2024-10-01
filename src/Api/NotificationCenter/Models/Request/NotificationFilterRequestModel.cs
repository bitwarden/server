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
    /// The start date. Must be less than the end date. Inclusive.
    /// </summary>
    public DateTime Start { get; set; } = DateTime.MinValue;

    /// <summary>
    /// The end date. Must be greater than the start date. Not inclusive.
    /// </summary>
    public DateTime End { get; set; } = DateTime.MaxValue;

    /// <summary>
    /// Number of items to return. Defaults to 10.
    /// </summary>
    public int PageSize { get; set; } = 10;
}
