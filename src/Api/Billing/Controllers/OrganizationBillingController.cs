using Bit.Api.Billing.Models.Responses;
using Bit.Core.Billing.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("organizations/{organizationId:guid}/billing")]
[Authorize("Application")]
public class OrganizationBillingController(
    IOrganizationBillingQueries organizationBillingQueries) : Controller
{
    [HttpGet("metadata")]
    public async Task<IResult> GetMetadataAsync([FromRoute] Guid organizationId)
    {
        var metadata = await organizationBillingQueries.GetMetadata(organizationId);

        if (metadata == null)
        {
            return TypedResults.NotFound();
        }

        var response = OrganizationMetadataResponse.From(metadata);

        return TypedResults.Ok(response);
    }
}
