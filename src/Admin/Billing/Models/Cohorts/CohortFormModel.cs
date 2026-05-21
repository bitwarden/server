using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;

namespace Bit.Admin.Billing.Models.Cohorts;

public class CohortFormModel
{
    public Guid? Id { get; set; }

    [Required]
    [MaxLength(255)]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Migration path")]
    public string MigrationPathSelection { get; set; } = string.Empty;

    [MaxLength(64)]
    [Display(Name = "Proactive discount coupon")]
    public string? ProactiveDiscountCouponCode { get; set; }

    [MaxLength(64)]
    [Display(Name = "Churn discount coupon")]
    public string? ChurnDiscountCouponCode { get; set; }

    public MigrationPathId? GetMigrationPathId()
    {
        if (MigrationPathSelection == "none")
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
}
