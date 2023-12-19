using Bit.Billing.Constants;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Bit.Billing.Services.Implementations;

public class ChargeRefundedHandler : StripeWebhookHandler
{
    protected override bool CanHandle(Event parsedEvent)
    {
        return parsedEvent.Type.Equals(HandledStripeWebhook.ChargeSucceeded);
    }

    protected override Task<IActionResult> ProcessEvent(Event parsedEvent) => throw new NotImplementedException();
}
