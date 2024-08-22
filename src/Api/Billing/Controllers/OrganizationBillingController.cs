using Bit.Api.Billing.Models.Responses;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("organizations/{organizationId:guid}/billing")]
[Authorize("Application")]
public class OrganizationBillingController(
    ICurrentContext currentContext,
    IOrganizationBillingService organizationBillingService,
    IOrganizationRepository organizationRepository,
    IPaymentService paymentService) : BaseBillingController
{
    [HttpGet("metadata")]
    public async Task<IResult> GetMetadataAsync([FromRoute] Guid organizationId)
    {
        if (!await currentContext.AccessMembersTab(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var metadata = await organizationBillingService.GetMetadata(organization);

        if (metadata == null)
        {
            return Error.NotFound();
        }

        var response = OrganizationMetadataResponse.From(metadata);

        return TypedResults.Ok(response);
    }

    [HttpGet("history")]
    public async Task<IResult> GetHistoryAsync([FromRoute] Guid organizationId)
    {
        if (!await currentContext.ViewBillingHistory(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var billingInfo = await paymentService.GetBillingHistoryAsync(organization);

        return TypedResults.Ok(billingInfo);
    }

    [HttpGet]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IResult> GetBillingAsync(Guid organizationId)
    {
        if (!await currentContext.ViewBillingHistory(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var billingInfo = await paymentService.GetBillingAsync(organization);

        var response = new BillingResponseModel(billingInfo);

        return TypedResults.Ok(response);
    }
}
