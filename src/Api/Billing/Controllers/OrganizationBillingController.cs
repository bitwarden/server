using Bit.Api.Billing.Models.Responses;
using Bit.Api.Models.Response;
using Bit.Core.Billing.Queries;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("organizations/{organizationId:guid}/billing")]
[Authorize("Application")]
public class OrganizationBillingController(
    IOrganizationBillingQueries organizationBillingQueries,
    ICurrentContext currentContext,
    IOrganizationRepository organizationRepository,
    IPaymentService paymentService) : Controller
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

    [HttpGet]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<BillingResponseModel> GetBilling(Guid organizationId)
    {
        if (!await currentContext.ViewBillingHistory(organizationId))
        {
            throw new NotFoundException();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        var billingInfo = await paymentService.GetBillingAsync(organization);
        return new BillingResponseModel(billingInfo);
    }
}
