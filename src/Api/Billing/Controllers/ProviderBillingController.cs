using Bit.Api.Billing.Models;
using Bit.Core;
using Bit.Core.Billing.Queries;
using Bit.Core.Context;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("providers/{providerId:guid}/billing")]
[Authorize("Application")]
public class ProviderBillingController(
    ICurrentContext currentContext,
    IFeatureService featureService,
    IProviderBillingQueries providerBillingQueries) : Controller
{
    [HttpGet("subscription")]
    public async Task<IResult> GetSubscriptionAsync([FromRoute] Guid providerId)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling))
        {
            return TypedResults.NotFound();
        }

        if (!currentContext.ProviderProviderAdmin(providerId))
        {
            return TypedResults.Unauthorized();
        }

        var subscriptionData = await providerBillingQueries.GetSubscriptionData(providerId);

        if (subscriptionData == null)
        {
            return TypedResults.NotFound();
        }

        var (providerPlans, subscription) = subscriptionData;

        var providerSubscriptionDTO = ProviderSubscriptionDTO.From(providerPlans, subscription);

        return TypedResults.Ok(providerSubscriptionDTO);
    }
}
