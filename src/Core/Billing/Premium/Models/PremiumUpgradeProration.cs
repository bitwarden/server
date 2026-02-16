namespace Bit.Core.Billing.Premium.Models;

/// <summary>
/// Represents the proration details for upgrading a Premium user subscription to an Organization plan.
/// </summary>
public class PremiumUpgradeProration
{
    /// <summary>
    /// The prorated cost for the new organization plan, calculated from now until the end of the current billing period.
    /// This represents what the user will pay for the upgraded plan for the remainder of the period.
    /// </summary>
    public decimal NewPlanProratedAmount { get; set; }

    /// <summary>
    /// The credit amount for the unused portion of the current Premium subscription.
    /// This credit is applied against the cost of the new organization plan.
    /// </summary>
    public decimal Credit { get; set; }

    /// <summary>
    /// The tax amount calculated for the upgrade transaction.
    /// </summary>
    public decimal Tax { get; set; }

    /// <summary>
    /// The total amount due for the upgrade after applying the credit and adding tax.
    /// </summary>
    public decimal Total { get; set; }

    /// <summary>
    /// The number of months the user will be charged for the new organization plan in the prorated billing period.
    /// Calculated by rounding the days remaining in the current billing cycle to the nearest month.
    /// Minimum value is 1 month (never returns 0).
    /// </summary>
    public int NewPlanProratedMonths { get; set; }
}
