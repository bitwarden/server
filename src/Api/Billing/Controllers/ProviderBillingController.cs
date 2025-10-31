// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Billing.Models.Requests;
using Bit.Api.Billing.Models.Responses;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Providers.Models;
using Bit.Core.Billing.Providers.Repositories;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Models;
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
    IPricingClient pricingClient,
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

        var invoices = await stripeAdapter.ListInvoicesAsync(new StripeInvoiceListOptions
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

    [HttpPut("payment-method")]
    public async Task<IResult> UpdatePaymentMethodAsync(
        [FromRoute] Guid providerId,
        [FromBody] UpdatePaymentMethodRequestBody requestBody)
    {
        var (provider, result) = await TryGetBillableProviderForAdminOperation(providerId);

        if (provider == null)
        {
            return result;
        }

        var tokenizedPaymentSource = requestBody.PaymentSource.ToDomain();
        var taxInformation = requestBody.TaxInformation.ToDomain();

        await providerBillingService.UpdatePaymentMethod(
            provider,
            tokenizedPaymentSource,
            taxInformation);

        return TypedResults.Ok();
    }

    [HttpPost("payment-method/verify-bank-account")]
    public async Task<IResult> VerifyBankAccountAsync(
        [FromRoute] Guid providerId,
        [FromBody] VerifyBankAccountRequestBody requestBody)
    {
        var (provider, result) = await TryGetBillableProviderForAdminOperation(providerId);

        if (provider == null)
        {
            return result;
        }

        if (requestBody.DescriptorCode.Length != 6 || !requestBody.DescriptorCode.StartsWith("SM"))
        {
            return Error.BadRequest("Statement descriptor should be a 6-character value that starts with 'SM'");
        }

        await subscriberService.VerifyBankAccount(provider, requestBody.DescriptorCode);

        return TypedResults.Ok();
    }

    [HttpGet("subscription")]
    public async Task<IResult> GetSubscriptionAsync([FromRoute] Guid providerId)
    {
        var (provider, result) = await TryGetBillableProviderForServiceUserOperation(providerId);

        if (provider == null)
        {
            return result;
        }

        var subscription = await stripeAdapter.GetSubscriptionAsync(provider.GatewaySubscriptionId,
            new SubscriptionGetOptions { Expand = ["customer.tax_ids", "discounts", "test_clock"] });

        var providerPlans = await providerPlanRepository.GetByProviderId(provider.Id);

        var configuredProviderPlans = await Task.WhenAll(providerPlans.Select(async providerPlan =>
        {
            var plan = await pricingClient.GetPlanOrThrow(providerPlan.PlanType);
            var priceId = ProviderPriceAdapter.GetPriceId(provider, subscription, plan.Type);
            var price = await stripeAdapter.GetPriceAsync(priceId);

            var unitAmount = price.UnitAmountDecimal.HasValue
                ? price.UnitAmountDecimal.Value / 100M
                : plan.PasswordManager.ProviderPortalSeatPrice;

            return new ConfiguredProviderPlan(
                providerPlan.Id,
                providerPlan.ProviderId,
                plan,
                unitAmount,
                providerPlan.SeatMinimum ?? 0,
                providerPlan.PurchasedSeats ?? 0,
                providerPlan.AllocatedSeats ?? 0);
        }));

        var taxInformation = GetTaxInformation(subscription.Customer);

        var subscriptionSuspension = await GetSubscriptionSuspensionAsync(stripeAdapter, subscription);

        var paymentSource = await subscriberService.GetPaymentSource(provider);

        var response = ProviderSubscriptionResponse.From(
            subscription,
            configuredProviderPlans,
            taxInformation,
            subscriptionSuspension,
            provider,
            paymentSource);

        return TypedResults.Ok(response);
    }

    [HttpGet("tax-information")]
    public async Task<IResult> GetTaxInformationAsync([FromRoute] Guid providerId)
    {
        var (provider, result) = await TryGetBillableProviderForAdminOperation(providerId);

        if (provider == null)
        {
            return result;
        }

        var taxInformation = await subscriberService.GetTaxInformation(provider);

        var response = TaxInformationResponse.From(taxInformation);

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
            requestBody.TaxIdType,
            requestBody.Line1,
            requestBody.Line2,
            requestBody.City,
            requestBody.State);

        await subscriberService.UpdateTaxInformation(provider, taxInformation);

        return TypedResults.Ok();
    }
}
