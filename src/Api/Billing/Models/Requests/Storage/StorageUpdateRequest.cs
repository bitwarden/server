using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Models.Requests.Storage;

/// <summary>
/// Request model for updating storage allocation on a user's premium subscription.
/// Allows for both increasing and decreasing storage in an idempotent manner.
/// </summary>
public class StorageUpdateRequest : IValidatableObject
{
    /// <summary>
    /// The desired total storage in GB (including base storage).
    /// Must be between the base storage amount and the maximum allowed (100 GB).
    /// </summary>
    [Required]
    [Range(1, 100)]
    public short StorageGb { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StorageGb <= 0)
        {
            yield return new ValidationResult(
                "Storage must be greater than 0 GB.",
                new[] { nameof(StorageGb) });
        }

        if (StorageGb > 100)
        {
            yield return new ValidationResult(
                "Maximum storage is 100 GB.",
                new[] { nameof(StorageGb) });
        }
    }
}
