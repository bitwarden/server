using Microsoft.AspNetCore.Mvc;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;


public abstract class StripeWebhookHandler
{
    public const string PremiumPlanId = "premium-annually";
    public const string PremiumPlanIdAppStore = "premium-annually-app";
    protected StripeWebhookHandler NextHandler { get; private set; }

    public void SetNextHandler(StripeWebhookHandler handler)
    {
        NextHandler = handler;
    }

    public async Task HandleRequest(Event parsedEvent)
    {
        if (CanHandle(parsedEvent))
        {
            await ProcessEvent(parsedEvent);
        }
        else if (NextHandler != null)
        {
            await NextHandler.HandleRequest(parsedEvent);
        }
    }

    protected abstract bool CanHandle(Event parsedEvent);
    protected abstract Task<IActionResult> ProcessEvent(Event parsedEvent);

}
