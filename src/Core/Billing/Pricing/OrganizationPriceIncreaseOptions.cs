namespace Bit.Core.Billing.Pricing;

/// <summary>
/// Controls optional guard behavior when scheduling an organization price increase via
/// <see cref="IPriceIncreaseScheduler.ScheduleForSubscription"/>. Guards are applied
/// during cohort validation before any Stripe calls are made.
/// </summary>
public record OrganizationPriceIncreaseOptions
{
    /// <summary>
    /// Skip scheduling if a price increase has already been scheduled for this
    /// organization (i.e. <c>assignment.ScheduledDate</c> is set).
    /// </summary>
    public bool SkipIfAlreadyScheduled { get; init; }
}
