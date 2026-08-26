// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Utilities;

namespace Bit.Api.Dirt.Public.Models;

public class EventFilterRequestModel
{
    /// <summary>
    /// The start date. If omitted, defaults to 30 days before the end date.
    /// </summary>
    public DateTime? Start { get; set; }
    /// <summary>
    /// The end date. If omitted, defaults to the end of the current day.
    /// </summary>
    public DateTime? End { get; set; }
    /// <summary>
    /// The unique identifier of the user that performed the event.
    /// </summary>
    public Guid? ActingUserId { get; set; }
    /// <summary>
    /// The unique identifier of the related item that the event describes.
    /// </summary>
    public Guid? ItemId { get; set; }
    /// <summary>
    /// The unique identifier of the related secret that the event describes.
    /// </summary>
    public Guid? SecretId { get; set; }
    /// <summary>
    /// The unique identifier of the related project that the event describes.
    /// </summary>
    public Guid? ProjectId { get; set; }
    /// <summary>
    /// A cursor for use in pagination.
    /// </summary>
    public string ContinuationToken { get; set; }

    public Tuple<DateTime, DateTime> ToDateRange()
    {
        var dateRange = ApiHelpers.GetDateRange(Start, End);
        Start = dateRange.Item1;
        End = dateRange.Item2;
        return dateRange;
    }
}
