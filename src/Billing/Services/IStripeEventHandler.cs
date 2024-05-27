using Event = Stripe.Event;
namespace Bit.Billing.Services;

public interface IStripeEventHandler
{
    Task HandleAsync(Event parsedEvent);
}

public interface ISubscriptionDeletedHandler : IStripeEventHandler;
public interface ISubscriptionUpdatedHandler : IStripeEventHandler;
public interface IUpcomingInvoiceHandler : IStripeEventHandler;
public interface IChargeSucceededHandler : IStripeEventHandler;
public interface IChargeRefundedHandler : IStripeEventHandler;
public interface IPaymentSucceededHandler : IStripeEventHandler;
public interface IPaymentFailedHandler : IStripeEventHandler;
public interface IInvoiceCreatedHandler : IStripeEventHandler;
public interface IPaymentMethodAttachedHandler : IStripeEventHandler;

public interface ICustomerUpdatedHandler : IStripeEventHandler;
