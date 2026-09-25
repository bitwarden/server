namespace Bit.Core.Billing.Organizations.Schedules.Enums;

/// <summary>
/// Who created the Stripe subscription schedule attached to an organization's subscription.
/// Operations that release or rewrite a schedule must not act on one our code did not create:
/// negotiated renewals are authored by hand in the Stripe Dashboard, and releasing one destroys
/// terms the billing team owns.
/// </summary>
public enum OrganizationSubscriptionScheduleOwnership
{
    /// <summary>No active schedule is attached to the subscription.</summary>
    None,

    /// <summary>A schedule created by redeeming the annual upgrade offer.</summary>
    AnnualUpgrade,

    /// <summary>A schedule created by the business plan price migration program.</summary>
    PriceMigration,

    /// <summary>A schedule our code did not create. Leave it alone.</summary>
    Foreign,

    /// <summary>
    /// The subscription reports a schedule the caller did not expand. A caller bug, not a data
    /// condition, and deliberately not None: a caller told None would release nothing and then
    /// create a second schedule, which Stripe rejects.
    /// </summary>
    Unexpanded
}
