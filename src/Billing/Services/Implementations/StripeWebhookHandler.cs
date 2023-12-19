using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;


public abstract class StripeWebhookHandler
{
    public const decimal PremiumPlanAppleIapPrice = 14.99M;
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

    public Tuple<Guid?, Guid?> GetIdsFromMetaData(IDictionary<string, string> metaData)
    {
        if (metaData == null || !metaData.Any())
        {
            return new Tuple<Guid?, Guid?>(null, null);
        }

        Guid? orgId = null;
        Guid? userId = null;

        if (metaData.ContainsKey("organizationId"))
        {
            orgId = new Guid(metaData["organizationId"]);
        }
        else if (metaData.ContainsKey("userId"))
        {
            userId = new Guid(metaData["userId"]);
        }

        if (userId == null && orgId == null)
        {
            var orgIdKey = metaData.Keys.FirstOrDefault(k => k.ToLowerInvariant() == "organizationid");
            if (!string.IsNullOrWhiteSpace(orgIdKey))
            {
                orgId = new Guid(metaData[orgIdKey]);
            }
            else
            {
                var userIdKey = metaData.Keys.FirstOrDefault(k => k.ToLowerInvariant() == "userid");
                if (!string.IsNullOrWhiteSpace(userIdKey))
                {
                    userId = new Guid(metaData[userIdKey]);
                }
            }
        }

        return new Tuple<Guid?, Guid?>(orgId, userId);
    }

    public static bool IsSponsoredSubscription(Subscription subscription) =>
        StaticStore.SponsoredPlans.Any(p => p.StripePlanId == subscription.Id);

    protected abstract bool CanHandle(Event parsedEvent);
    protected abstract Task<IActionResult> ProcessEvent(Event parsedEvent);

}
