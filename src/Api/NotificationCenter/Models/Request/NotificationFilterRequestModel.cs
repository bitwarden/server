#nullable enable
using System.ComponentModel.DataAnnotations;

namespace Bit.Api.NotificationCenter.Models.Request;

public class NotificationFilterRequestModel : IValidatableObject
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
    [StringLength(9)]
    public string? ContinuationToken { get; set; }

    /// <summary>
    /// The number of items to return in a single page.
    /// Default 10. Minimum 10, maximum 1000.
    /// </summary>
    [Range(10, 1000)]
    public int PageSize { get; set; } = 10;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(ContinuationToken) &&
            (!int.TryParse(ContinuationToken, out var pageNumber) || pageNumber <= 0))
        {
            yield return new ValidationResult(
                "Continuation token must be a positive, non zero integer.",
                [nameof(ContinuationToken)]);
        }
    }
}
