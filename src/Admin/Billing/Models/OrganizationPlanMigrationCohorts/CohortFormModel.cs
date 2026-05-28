using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;

namespace Bit.Admin.Billing.Models.OrganizationPlanMigrationCohorts;

public class CohortFormModel : IValidatableObject
{
    public const string NoMigrationPath = "none";

    public Guid? Id { get; set; }

    [Required]
    [MaxLength(255)]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a migration path or None.")]
    [Display(Name = "Migration path")]
    public string MigrationPathSelection { get; set; } = string.Empty;

    [MaxLength(64)]
    [Display(Name = "Proactive discount coupon")]
    public string? ProactiveDiscountCouponCode { get; set; }

    [MaxLength(64)]
    [Display(Name = "Churn discount coupon")]
    public string? ChurnDiscountCouponCode { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; }

    public MigrationPathId? GetMigrationPathId()
    {
        if (MigrationPathSelection == NoMigrationPath)
        {
            return null;
        }

        if (byte.TryParse(MigrationPathSelection, out var id))
        {
            return (MigrationPathId)id;
        }

        throw new InvalidOperationException(
            $"MigrationPathSelection '{MigrationPathSelection}' cannot be converted to MigrationPathId.");
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MigrationPathSelection != NoMigrationPath)
            yield break;

        if (!string.IsNullOrEmpty(ProactiveDiscountCouponCode))
        {
            yield return new ValidationResult(
                "Churn-only cohorts cannot have a proactive discount coupon.",
                [nameof(ProactiveDiscountCouponCode)]);
        }

        if (string.IsNullOrEmpty(ChurnDiscountCouponCode))
        {
            yield return new ValidationResult(
                "Churn discount coupon is required for Churn-only cohorts.",
                [nameof(ChurnDiscountCouponCode)]);
        }
    }

    public static CohortFormModel From(OrganizationPlanMigrationCohort cohort) => new()
    {
        Id = cohort.Id,
        Name = cohort.Name,
        MigrationPathSelection = cohort.MigrationPathId switch
        {
            null => NoMigrationPath,
            var id => ((byte)id).ToString(),
        },
        ProactiveDiscountCouponCode = cohort.ProactiveDiscountCouponCode,
        ChurnDiscountCouponCode = cohort.ChurnDiscountCouponCode,
        IsActive = cohort.IsActive,
    };
}
