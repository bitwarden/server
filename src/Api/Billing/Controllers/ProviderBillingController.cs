using Bit.Api.Billing.Models.Requests;
using Bit.Api.Billing.Models.Responses;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Models;
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
    IProviderBillingService providerBillingService,
    IProviderRepository providerRepository,
    ISubscriberService subscriberService) : BaseProviderController(currentContext, featureService, providerRepository)
{
    [HttpGet("invoices")]
    public async Task<IResult> GetInvoicesAsync([FromRoute] Guid providerId)
    {
        var (provider, result) = await TryGetBillableProviderForAdminOperation(providerId);

        if (provider == null)
        {
            return result;
        }

        var invoices = await subscriberService.GetInvoices(provider);

        var response = InvoicesResponse.From(invoices);

        return TypedResults.Ok(response);
    }

    [HttpGet("invoices/{invoiceId}")]
    public async Task<IResult> GenerateClientInvoiceReportAsync([FromRoute] Guid providerId, string invoiceId)
    {
        var (provider, result) = await TryGetBillableProviderForAdminOperation(providerId);

        if (provider == null)
        {
            return result;
        }

        var reportContent = await providerBillingService.GenerateClientInvoiceReport(invoiceId);

        if (reportContent == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.File(
            reportContent,
            "text/csv");
    }

    [HttpGet("subscription")]
    public async Task<IResult> GetSubscriptionAsync([FromRoute] Guid providerId)
    {
        var (provider, result) = await TryGetBillableProviderForServiceUserOperation(providerId);

        if (provider == null)
        {
            return result;
        }

        var consolidatedBillingSubscription = await providerBillingService.GetConsolidatedBillingSubscription(provider);

        if (consolidatedBillingSubscription == null)
        {
            return TypedResults.NotFound();
        }

        var response = ConsolidatedBillingSubscriptionResponse.From(consolidatedBillingSubscription);

        return TypedResults.Ok(response);
    }

    [HttpPut("tax-information")]
    public async Task<IResult> UpdateTaxInformationAsync(
        [FromRoute] Guid providerId,
        [FromBody] TaxInformationRequestBody requestBody)
    {
        var (provider, result) = await TryGetBillableProviderForAdminOperation(providerId);

        if (provider == null)
        {
            return result;
        }

        var taxInformation = new TaxInformationDTO(
            requestBody.Country,
            requestBody.PostalCode,
            requestBody.TaxId,
            requestBody.Line1,
            requestBody.Line2,
            requestBody.City,
            requestBody.State);

        await subscriberService.UpdateTaxInformation(provider, taxInformation);

        return TypedResults.Ok();
    }
}
