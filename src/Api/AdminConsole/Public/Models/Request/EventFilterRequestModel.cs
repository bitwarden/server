using Bit.Core.Exceptions;

namespace Bit.Api.Models.Public.Request;

public class EventFilterRequestModel
{
    /// <summary>
    /// The start date. Must be less than the end date.
    /// </summary>
    public DateTime? Start { get; set; }

    /// <summary>
    /// The end date. Must be greater than the start date.
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
    /// A cursor for use in pagination.
    /// </summary>
    public string ContinuationToken { get; set; }

    public Tuple<DateTime, DateTime> ToDateRange()
    {
        if (!End.HasValue || !Start.HasValue)
        {
            End = DateTime.UtcNow.Date.AddDays(1).AddMilliseconds(-1);
            Start = DateTime.UtcNow.Date.AddDays(-30);
        }
        else if (Start.Value > End.Value)
        {
            var newEnd = Start;
            Start = End;
            End = newEnd;
        }

        if ((End.Value - Start.Value) > TimeSpan.FromDays(367))
        {
            throw new BadRequestException("Date range must be < 367 days.");
        }

        return new Tuple<DateTime, DateTime>(Start.Value, End.Value);
    }
}
