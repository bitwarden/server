namespace Bit.Billing.Constants;

public static class StripeSubscriptionStatus
{
    /// <summary>
    /// The subscription is currently in a trial period and it’s safe to provision your product for your customer.
    /// The subscription transitions automatically to <see cref="StripeSubscriptionStatus.Active">active</see> when the
    /// first payment is made.
    /// </summary>
    public const string Trialing = "trialing";

    /// <summary>
    /// The subscription is in good standing and the most recent payment is successful. It’s safe to provision your
    /// product for your customer.
    /// </summary>
    public const string Active = "active";

    /// <summary>
    /// A successful payment needs to be made within 23 hours to activate the subscription. Or the payment requires
    /// action, like customer authentication. Read more about payments that
    /// <a href="https://stripe.com/docs/billing/subscriptions/overview#requires-action">require action.</a>
    /// Subscriptions can also be <see cref="StripeSubscriptionStatus.Incomplete">incomplete</see> if there’s a pending
    /// payment. In that case, the invoice status would be <c>open_payment_pending</c> and the PaymentIntent status
    /// would be <c>processing</c>.
    /// </summary>
    public const string Incomplete = "incomplete";

    /// <summary>
    /// The initial payment on the subscription failed and no successful payment was made within 23 hours of creating
    /// the subscription. These subscriptions don’t bill customers. This status exists so you can track customers that
    /// failed to activate their subscriptions.
    /// </summary>
    public const string IncompleteExpired = "incomplete_expired";

    /// <summary>
    /// Payment on the latest <i>finalized</i> invoice either failed or wasn’t attempted. The subscription continues to
    /// create invoices. Your <a href="https://stripe.com/docs/billing/subscriptions/overview#settings">subscription
    /// settings</a> determine the subscription’s next state. If the invoice is still unpaid after all
    /// <a href="https://stripe.com/docs/billing/revenue-recovery/smart-retries">Smart Retries</a> have been attempted,
    /// you can configure the subscription to move to <see cref="StripeSubscriptionStatus.Canceled">canceled</see>,
    /// <see cref="StripeSubscriptionStatus.Unpaid">unpaid</see>, or leave it as
    /// <see cref="StripeSubscriptionStatus.PastDue">past_due</see>. To move the subscription to
    /// <see cref="StripeSubscriptionStatus.Active">active</see>, pay the most recent invoice before its due date.
    /// </summary>
    public const string PastDue = "past_due";

    /// <summary>
    /// The subscription has been canceled. During cancellation, automatic collection for all unpaid invoices is
    /// disabled (<c>auto_advance=false</c>). This is a terminal state that can’t be updated.
    /// </summary>
    public const string Canceled = "canceled";

    /// <summary>
    /// The latest invoice hasn’t been paid but the subscription remains in place. The latest invoice remains open and
    /// invoices continue to be generated but payments aren’t attempted. You should revoke access to your product when
    /// the subscription is <see cref="StripeSubscriptionStatus.Unpaid">unpaid</see> since payments were already
    /// attempted and retried when it was <see cref="StripeSubscriptionStatus.PastDue">past_due</see>. To move the
    /// subscription to <see cref="StripeSubscriptionStatus.Active">active</see>, pay the most recent invoice before its
    /// due date.
    /// </summary>
    public const string Unpaid = "unpaid";

    /// <summary>
    /// The subscription has ended its trial period without a default payment method and the
    /// <c>trial_settings.end_behavior.missing_payment_method</c> is set to <c>pause</c>. Invoices will no longer be
    /// created for the subscription. After a default payment method has been attached to the customer, you can
    /// <a href="https://stripe.com/docs/billing/subscriptions/trials#resume-a-paused-subscription">resume the
    /// subscription.</a>
    /// </summary>
    public const string Paused = "paused";
}
