using Bit.Api.Billing.Models.Requests;
using Bit.Api.Billing.Models.Responses;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("organizations/{organizationId:guid}/billing")]
[Authorize("Application")]
public class OrganizationBillingController(
    IBusinessUnitConverter businessUnitConverter,
    ICurrentContext currentContext,
    IOrganizationBillingService organizationBillingService,
    IOrganizationRepository organizationRepository,
    IStripePaymentService paymentService,
    IPaymentHistoryService paymentHistoryService) : BaseBillingController
{
    // TODO: Remove when pm-25379-use-new-organization-metadata-structure is removed.
    [HttpGet("metadata")]
    public async Task<IResult> GetMetadataAsync([FromRoute] Guid organizationId)
    {
        if (!await currentContext.OrganizationUser(organizationId))
        {
            return Error.Unauthorized();
        }

        var metadata = await organizationBillingService.GetMetadata(organizationId);

        if (metadata == null)
        {
            return Error.NotFound();
        }

        return TypedResults.Ok(metadata);
    }

    // TODO: Migrate to Query / OrganizationBillingVNextController
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

    // TODO: Migrate to Query / OrganizationBillingVNextController
    [HttpGet("invoices")]
    public async Task<IResult> GetInvoicesAsync([FromRoute] Guid organizationId, [FromQuery] string? status = null, [FromQuery] string? startAfter = null)
    {
        if (!await currentContext.ViewBillingHistory(organizationId))
        {
            return TypedResults.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return TypedResults.NotFound();
        }

        var invoices = await paymentHistoryService.GetInvoiceHistoryAsync(
            organization,
            5,
            status,
            startAfter);

        return TypedResults.Ok(invoices);
    }

    // TODO: Migrate to Query / OrganizationBillingVNextController
    [HttpGet("transactions")]
    public async Task<IResult> GetTransactionsAsync([FromRoute] Guid organizationId, [FromQuery] DateTime? startAfter = null)
    {
        if (!await currentContext.ViewBillingHistory(organizationId))
        {
            return TypedResults.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return TypedResults.NotFound();
        }

        var transactions = await paymentHistoryService.GetTransactionHistoryAsync(
            organization,
            5,
            startAfter);

        return TypedResults.Ok(transactions);
    }

    // TODO: Can be removed once we do away with the organization-plans.component.
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

    // TODO: Migrate to Command / OrganizationBillingVNextController
    [HttpPost("setup-business-unit")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IResult> SetupBusinessUnitAsync(
        [FromRoute] Guid organizationId,
        [FromBody] SetupBusinessUnitRequestBody requestBody)
    {
        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        if (!await currentContext.OrganizationUser(organizationId))
        {
            return Error.Unauthorized();
        }

        var providerId = await businessUnitConverter.FinalizeConversion(
            organization,
            requestBody.UserId,
            requestBody.Token,
            requestBody.ProviderKey,
            requestBody.OrganizationKey);

        return TypedResults.Ok(providerId);
    }

    // TODO: Migrate to Command / OrganizationBillingVNextController
    [HttpPost("change-frequency")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IResult> ChangePlanSubscriptionFrequencyAsync(
        [FromRoute] Guid organizationId,
        [FromBody] ChangePlanFrequencyRequest request)
    {
        if (!await currentContext.EditSubscription(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        if (organization.PlanType == request.NewPlanType)
        {
            return Error.BadRequest("Organization is already on the requested plan frequency.");
        }

        await organizationBillingService.UpdateSubscriptionPlanFrequency(
            organization,
            request.NewPlanType);

        return TypedResults.Ok();
    }
}
