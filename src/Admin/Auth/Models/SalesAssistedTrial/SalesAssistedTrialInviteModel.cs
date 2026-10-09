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
    [MinLength(1, ErrorMessage = "Select at least one product.")]
    public List<ProductType> Products { get; set; } = [];

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

        if (ProductTier == ProductTierType.Families && Products.Contains(ProductType.SecretsManager))
        {
            // Current constraint of Families plan, hard-coded validation here for
            // fail-fast feedback to tool users.
            // PM-41426
            yield return new ValidationResult(
                "Secrets Manager is not available for the Families plan.",
                [nameof(Products)]);
        }

        if (Products.Contains(ProductType.PasswordManager) && Products.Contains(ProductType.SecretsManager))
        {
            yield return new ValidationResult(
                "A Secrets Manager trial already includes Password Manager; select Secrets Manager on its own.",
                [nameof(Products)]);
        }

        if (Products.Contains(ProductType.PrivilegedControls))
        {
            if (!Products.Contains(ProductType.PasswordManager))
            {
                yield return new ValidationResult(
                    "Privileged Controls requires Password Manager.",
                    [nameof(Products)]);
            }

            if (ProductTier != ProductTierType.Enterprise)
            {
                yield return new ValidationResult(
                    "Privileged Controls is only available on Password Manager Enterprise.",
                    [nameof(ProductTier)]);
            }

            if (Products.Contains(ProductType.SecretsManager))
            {
                yield return new ValidationResult(
                    "Privileged Controls cannot be combined with Secrets Manager.",
                    [nameof(Products)]);
            }
        }
    }
}
