using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Models.Requests.Storage;

/// <summary>
/// Request model for updating storage allocation on a user's premium subscription.
/// Allows for both increasing and decreasing storage in an idempotent manner.
/// </summary>
public class StorageUpdateRequest : IValidatableObject
{
    /// <summary>
    /// The additional storage in GB beyond the base storage.
    /// Must be between 0 and the maximum allowed (minus base storage).
    /// </summary>
    [Required]
    [Range(0, 99)]
    public short AdditionalStorageGb { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AdditionalStorageGb < 0)
        {
            yield return new ValidationResult(
                "Additional storage cannot be negative.",
                new[] { nameof(AdditionalStorageGb) });
        }

        if (AdditionalStorageGb > 99)
        {
            yield return new ValidationResult(
                "Maximum additional storage is 99 GB.",
                new[] { nameof(AdditionalStorageGb) });
        }
    }
}
