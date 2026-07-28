namespace Bit.Core.Billing.Organizations.Schedules.Enums;

/// <summary>
/// Who created the Stripe subscription schedule attached to an organization's subscription.
/// Operations that would release or rewrite a schedule must not act on one Bitwarden did not
/// create: Finance authors negotiated renewals by hand in the Stripe Dashboard, and releasing
/// one silently destroys terms the billing team owns.
/// </summary>
public enum OrganizationSubscriptionScheduleOwnership
{
    /// <summary>No active schedule is attached to the subscription.</summary>
    None,

    /// <summary>A schedule created by redeeming the annual upgrade offer.</summary>
    AnnualUpgrade,

    /// <summary>A schedule created by the business plan price migration program.</summary>
    PriceMigration,

    /// <summary>A schedule Bitwarden did not create. Leave it alone.</summary>
    Foreign
}
