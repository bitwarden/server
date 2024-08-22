using Bit.Api.Billing.Models.Requests;
using Bit.Api.Billing.Models.Responses;
using Bit.Core.Billing.Models;
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
    IPaymentService paymentService,
    ISubscriberService subscriberService) : BaseBillingController
{
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

    [HttpGet("payment-method")]
    public async Task<IResult> GetPaymentMethodAsync([FromRoute] Guid organizationId)
    {
        if (!await currentContext.EditPaymentMethods(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var paymentMethod = await subscriberService.GetPaymentMethod(organization);

        var response = PaymentMethodResponse.From(paymentMethod);

        return TypedResults.Ok(response);
    }

    [HttpPut("payment-method")]
    public async Task<IResult> UpdatePaymentMethodAsync(
        [FromRoute] Guid organizationId,
        [FromBody] UpdatePaymentMethodRequestBody requestBody)
    {
        if (!await currentContext.EditPaymentMethods(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var tokenizedPaymentSource = requestBody.GetTokenizedPaymentSource();

        await subscriberService.UpdatePaymentSource(organization, tokenizedPaymentSource);

        var taxInformation = requestBody.GetTaxInformation();

        await subscriberService.UpdateTaxInformation(organization, taxInformation);

        return TypedResults.Ok();
    }

    [HttpPost("payment-source/verify-bank-account")]
    public async Task<IResult> VerifyBankAccountAsync(
        [FromRoute] Guid organizationId,
        [FromBody] VerifyBankAccountRequestBody requestBody)
    {
        if (!await currentContext.EditPaymentMethods(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        await subscriberService.VerifyBankAccount(organization, (requestBody.Amount1, requestBody.Amount2));

        return TypedResults.Ok();
    }

    [HttpPut("tax-information")]
    public async Task<IResult> UpdateTaxInformationAsync(
        [FromRoute] Guid organizationId,
        [FromBody] TaxInformationRequestBody requestBody)
    {
        if (!await currentContext.EditPaymentMethods(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var taxInformation = requestBody.GetTaxInformation();

        await subscriberService.UpdateTaxInformation(organization, taxInformation);

        return TypedResults.Ok();
    }
}
