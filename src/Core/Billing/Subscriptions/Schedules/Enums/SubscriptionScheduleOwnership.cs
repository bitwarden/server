namespace Bit.Core.Billing.Subscriptions.Schedules.Enums;

/// <summary>
/// Who created the Stripe subscription schedule attached to a subscription.
/// Operations that release or rewrite a schedule must not act on one our code did not create:
/// negotiated renewals are authored by hand in the Stripe Dashboard, and releasing one destroys
/// terms the billing team owns.
/// </summary>
public enum SubscriptionScheduleOwnership
{
    /// <summary>No active schedule is attached to the subscription.</summary>
    None,

    /// <summary>A schedule created by redeeming the annual upgrade offer.</summary>
    AnnualUpgrade,

    /// <summary>A schedule created by the business plan price increase program.</summary>
    BusinessPriceIncrease,

    /// <summary>A schedule our code did not create. Leave it alone.</summary>
    Foreign,

    /// <summary>
    /// The subscription reports a schedule the caller did not expand. A caller bug, not a data
    /// condition, and deliberately not None: a caller told None would release nothing and then
    /// create a second schedule, which Stripe rejects.
    /// </summary>
    Unexpanded,

    /// <summary>A schedule created by the Premium or Families price increase.</summary>
    PersonalPriceIncrease,

    /// <summary>
    /// A schedule carrying a managing-system marker we do not recognize: empty, malformed, or written by a
    /// version of our code this one does not know about. Leave it alone, as with <see cref="Foreign"/>, but
    /// surface it so the marker can be investigated.
    /// </summary>
    Unrecognized
}
