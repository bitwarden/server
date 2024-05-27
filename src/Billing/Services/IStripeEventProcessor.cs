using Event = Stripe.Event;
namespace Bit.Billing.Services;

public interface IStripeEventProcessor
{
    Task ProcessEventAsync(Event stripeEvent);
}
