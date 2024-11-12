using Bit.Api.Billing.Models.Requests;
using Bit.Api.Billing.Models.Responses;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Models.BitStripe;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

using static Bit.Core.Billing.Utilities;

namespace Bit.Api.Billing.Controllers;

[Route("providers/{providerId:guid}/billing")]
[Authorize("Application")]
public class ProviderBillingController(
    ICurrentContext currentContext,
    ILogger<BaseProviderController> logger,
    IProviderBillingService providerBillingService,
    IProviderPlanRepository providerPlanRepository,
    IProviderRepository providerRepository,
    ISubscriberService subscriberService,
    IStripeAdapter stripeAdapter,
    IUserService userService) : BaseProviderController(currentContext, logger, providerRepository, userService)
{
    [HttpGet("invoices")]
    public async Task<IResult> GetInvoicesAsync([FromRoute] Guid providerId)
    {
        var (provider, result) = await TryGetBillableProviderForAdminOperation(providerId);

        if (provider == null)
        {
            return result;
        }

        var invoices = await stripeAdapter.InvoiceListAsync(new StripeInvoiceListOptions
        {
            Customer = provider.GatewayCustomerId
        });

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
            return Error.ServerError("We had a problem generating your invoice CSV. Please contact support.");
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

        var subscription = await stripeAdapter.SubscriptionGetAsync(provider.GatewaySubscriptionId,
            new SubscriptionGetOptions { Expand = ["customer.tax_ids", "test_clock"] });

        var providerPlans = await providerPlanRepository.GetByProviderId(provider.Id);

        var taxInformation = GetTaxInformation(subscription.Customer);

        var subscriptionSuspension = await GetSubscriptionSuspensionAsync(stripeAdapter, subscription);

        var response = ProviderSubscriptionResponse.From(
            subscription,
            providerPlans,
            taxInformation,
            subscriptionSuspension,
            provider);

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

        if (requestBody is not { Country: not null, PostalCode: not null })
        {
            return Error.BadRequest("Country and postal code are required to update your tax information.");
        }

        var taxInformation = new TaxInformation(
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
