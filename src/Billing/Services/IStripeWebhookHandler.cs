using Event = Stripe.Event;

namespace Bit.Billing.Services;

public interface IStripeWebhookHandler
{
    /// <summary>
    /// Handles the specified Stripe event asynchronously.
    /// </summary>
    /// <param name="parsedEvent">The Stripe event to be handled.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleAsync(Event parsedEvent);
}

/// <summary>
/// Defines the contract for handling Stripe subscription deleted events.
/// </summary>
public interface ISubscriptionDeletedHandler : IStripeWebhookHandler;

/// <summary>
/// Defines the contract for handling Stripe subscription updated events.
/// </summary>
public interface ISubscriptionUpdatedHandler : IStripeWebhookHandler;

/// <summary>
/// Defines the contract for handling Stripe upcoming invoice events.
/// </summary>
public interface IUpcomingInvoiceHandler : IStripeWebhookHandler;

/// <summary>
/// Defines the contract for handling Stripe charge succeeded events.
/// </summary>
public interface IChargeSucceededHandler : IStripeWebhookHandler;

/// <summary>
/// Defines the contract for handling Stripe charge refunded events.
/// </summary>
public interface IChargeRefundedHandler : IStripeWebhookHandler;

/// <summary>
/// Defines the contract for handling Stripe payment succeeded events.
/// </summary>
public interface IPaymentSucceededHandler : IStripeWebhookHandler;

/// <summary>
/// Defines the contract for handling Stripe payment failed events.
/// </summary>
public interface IPaymentFailedHandler : IStripeWebhookHandler;

/// <summary>
/// Defines the contract for handling Stripe invoice created events.
/// </summary>
public interface IInvoiceCreatedHandler : IStripeWebhookHandler;

/// <summary>
/// Defines the contract for handling Stripe payment method attached events.
/// </summary>
public interface IPaymentMethodAttachedHandler : IStripeWebhookHandler;

/// <summary>
/// Defines the contract for handling Stripe customer updated events.
/// </summary>
public interface ICustomerUpdatedHandler : IStripeWebhookHandler;

/// <summary>
/// Defines the contract for handling Stripe Invoice Finalized events.
/// </summary>
public interface IInvoiceFinalizedHandler : IStripeWebhookHandler;
