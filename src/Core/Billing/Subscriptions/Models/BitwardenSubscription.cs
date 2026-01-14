namespace Bit.Core.Billing.Subscriptions.Models;

public record BitwardenSubscription
{
    /// <summary>
    /// The status of the subscription.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// The subscription's cart, including line items, any discounts, and estimated tax.
    /// </summary>
    public required Cart Cart { get; init; }

    /// <summary>
    /// The amount of storage available and used for the subscription.
    /// <remarks>Allowed Subscribers: User, Organization</remarks>
    /// </summary>
    public Storage? Storage { get; init; }

    /// <summary>
    /// If the subscription is pending cancellation, the date at which the
    /// subscription will be canceled.
    /// <remarks>Allowed Statuses: 'trialing', 'active'</remarks>
    /// </summary>
    public DateTime? CancelAt { get; init; }

    /// <summary>
    /// The date the subscription was canceled.
    /// <remarks>Allowed Statuses: 'canceled'</remarks>
    /// </summary>
    public DateTime? Canceled { get; init; }

    /// <summary>
    /// The date of the next charge for the subscription.
    /// <remarks>Allowed Statuses: 'trialing', 'active'</remarks>
    /// </summary>
    public DateTime? NextCharge { get; init; }

    /// <summary>
    /// The date the subscription will be or was suspended due to lack of payment.
    /// <remarks>Allowed Statuses: 'incomplete', 'incomplete_expired', 'past_due', 'unpaid'</remarks>
    /// </summary>
    public DateTime? Suspension { get; init; }

    /// <summary>
    /// The number of days after the subscription goes 'past_due' the subscriber has to resolve their
    /// open invoices before the subscription is suspended.
    /// <remarks>Allowed Statuses: 'past_due'</remarks>
    /// </summary>
    public int? GracePeriod { get; init; }
}
