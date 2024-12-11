using Event = Stripe.Event;

namespace Bit.Billing.Services;

public interface IStripeEventProcessor
{
    /// <summary>
    /// Processes the specified Stripe event asynchronously.
    /// </summary>
    /// <param name="parsedEvent">The Stripe event to be processed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProcessEventAsync(Event parsedEvent);
}
