using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;

namespace Bit.Admin.Auth.Models.SalesAssistedTrial;

public class SalesTrialInviteModel : IValidatableObject
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    public string? Name { get; set; }

    [Display(Name = "Product Tier")]
    [Required]
    public ProductTierType ProductTier { get; set; }

    [Required]
    [MinLength(1)]
    public IEnumerable<ProductType> Products { get; set; } = null!;

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
    }
}
