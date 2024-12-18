#nullable enable
using Bit.Api.Billing.Models.Requests;
using Bit.Api.Billing.Models.Responses;
using Bit.Core;
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
    IFeatureService featureService,
    IOrganizationBillingService organizationBillingService,
    IOrganizationRepository organizationRepository,
    IPaymentService paymentService,
    ISubscriberService subscriberService,
    IPaymentHistoryService paymentHistoryService) : BaseBillingController
{
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

    [HttpGet("payment-method")]
    public async Task<IResult> GetPaymentMethodAsync([FromRoute] Guid organizationId)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.AC2476_DeprecateStripeSourcesAPI))
        {
            return Error.NotFound();
        }

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
        if (!featureService.IsEnabled(FeatureFlagKeys.AC2476_DeprecateStripeSourcesAPI))
        {
            return Error.NotFound();
        }

        if (!await currentContext.EditPaymentMethods(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var tokenizedPaymentSource = requestBody.PaymentSource.ToDomain();

        var taxInformation = requestBody.TaxInformation.ToDomain();

        await organizationBillingService.UpdatePaymentMethod(organization, tokenizedPaymentSource, taxInformation);

        return TypedResults.Ok();
    }

    [HttpPost("payment-method/verify-bank-account")]
    public async Task<IResult> VerifyBankAccountAsync(
        [FromRoute] Guid organizationId,
        [FromBody] VerifyBankAccountRequestBody requestBody)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.AC2476_DeprecateStripeSourcesAPI))
        {
            return Error.NotFound();
        }

        if (!await currentContext.EditPaymentMethods(organizationId))
        {
            return Error.Unauthorized();
        }

        if (requestBody.DescriptorCode.Length != 6 || !requestBody.DescriptorCode.StartsWith("SM"))
        {
            return Error.BadRequest("Statement descriptor should be a 6-character value that starts with 'SM'");
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        await subscriberService.VerifyBankAccount(organization, requestBody.DescriptorCode);

        return TypedResults.Ok();
    }

    [HttpGet("tax-information")]
    public async Task<IResult> GetTaxInformationAsync([FromRoute] Guid organizationId)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.AC2476_DeprecateStripeSourcesAPI))
        {
            return Error.NotFound();
        }

        if (!await currentContext.EditPaymentMethods(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var taxInformation = await subscriberService.GetTaxInformation(organization);

        var response = TaxInformationResponse.From(taxInformation);

        return TypedResults.Ok(response);
    }

    [HttpPut("tax-information")]
    public async Task<IResult> UpdateTaxInformationAsync(
        [FromRoute] Guid organizationId,
        [FromBody] TaxInformationRequestBody requestBody)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.AC2476_DeprecateStripeSourcesAPI))
        {
            return Error.NotFound();
        }

        if (!await currentContext.EditPaymentMethods(organizationId))
        {
            return Error.Unauthorized();
        }

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            return Error.NotFound();
        }

        var taxInformation = requestBody.ToDomain();

        await subscriberService.UpdateTaxInformation(organization, taxInformation);

        return TypedResults.Ok();
    }
}
