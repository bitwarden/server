using Bit.Api.Billing.Models.Responses;
using Bit.Core.Billing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("organizations/{organizationId:guid}/billing")]
[Authorize("Application")]
public class OrganizationBillingController(
    IOrganizationBillingService organizationBillingService) : Controller
{
    [HttpGet("metadata")]
    public async Task<IResult> GetMetadataAsync([FromRoute] Guid organizationId)
    {
        var metadata = await organizationBillingService.GetMetadata(organizationId);

        if (metadata == null)
        {
            return TypedResults.NotFound();
        }

        var response = OrganizationMetadataResponse.From(metadata);

        return TypedResults.Ok(response);
    }
}
