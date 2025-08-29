// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Billing.Models.Responses;
using Bit.Commercial.Core.Billing.Providers.Services;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Providers.Models;
using Bit.Core.Billing.Providers.Repositories;
using Bit.Core.Billing.Providers.Services;
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

        var configuredProviderPlans = await Task.WhenAll(providerPlans.Select(async providerPlan =>
        {
            var plan = await pricingClient.GetPlanOrThrow(providerPlan.PlanType);
            var priceId = ProviderPriceAdapter.GetPriceId(provider, subscription, plan.Type);
            var price = await stripeAdapter.PriceGetAsync(priceId);

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
}
