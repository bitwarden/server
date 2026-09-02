using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;

namespace Bit.Admin.Auth.Models.SalesAssistedTrial;

public class SalesAssistedTrialInviteModel : IValidatableObject
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    public string? Name { get; set; }

    [Display(Name = "Product Tier")]
    [Required]
    public ProductTierType ProductTier { get; set; }

    [Required]
    public ProductType Product { get; set; }

    [Display(Name = "Trial Length (Days)")]
    [Required]
    [Range(1, 30)]
    public int TrialLength { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProductTier == ProductTierType.TeamsStarter)
        {
            yield return new ValidationResult(
                "Teams Starter is no longer available for new trials.",
                [nameof(ProductTier)]);
        }

        if (ProductTier == ProductTierType.Families && Product == ProductType.SecretsManager)
        {
            // Current constraint of Families plan, hard-coded validation here for
            // fail-fast feedback to tool users.
            // PM-41426
            yield return new ValidationResult(
                "Secrets Manager is not available for the Families plan.",
                [nameof(Product)]);
        }
    }
}
