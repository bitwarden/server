using Bit.Api.Billing.Models.Responses;
using Bit.Core;
using Bit.Core.Billing.Services;
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
    IProviderBillingService providerBillingService) : Controller
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

        var providerSubscriptionDTO = await providerBillingService.GetSubscriptionDTO(providerId);

        if (providerSubscriptionDTO == null)
        {
            return TypedResults.NotFound();
        }

        var (providerPlans, subscription) = providerSubscriptionDTO;

        var providerSubscriptionResponse = ProviderSubscriptionResponse.From(providerPlans, subscription);

        return TypedResults.Ok(providerSubscriptionResponse);
    }
}
