namespace Bit.Billing.Services;
using Event = Stripe.Event;
public interface IWebhookEventHandler
{
    bool CanHandle(Event parsedEvent);
    Task HandleAsync(Event parsedEvent);
}
