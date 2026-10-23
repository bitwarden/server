using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.Billing.Pricing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("plans")]
[Authorize("Application")]
public class PlansController(
    IPricingClient pricingClient,
    Bitwarden.Server.Sdk.Features.IFeatureService featureService) : Controller
{
    [HttpGet("")]
    [AllowAnonymous]
    public async Task<ListResponseModel<PlanResponseModel>> Get()
    {
        var plans = await pricingClient.ListPlans();
        var includePrivilegedControls = featureService.IsEnabled(FeatureFlagKeys.PM39883_Pam_Billing_Support);
        var responses = plans.Select(plan => new PlanResponseModel(plan, includePrivilegedControls: includePrivilegedControls));
        return new ListResponseModel<PlanResponseModel>(responses);
    }

    [HttpGet("premium")]
    public async Task<IResult> GetPremiumPlanAsync()
    {
        var premiumPlan = await pricingClient.GetAvailablePremiumPlan();
        return TypedResults.Ok(premiumPlan);
    }
}
