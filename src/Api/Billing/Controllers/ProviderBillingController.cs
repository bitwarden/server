using Bit.Api.Billing.Models.Requests;
using Bit.Api.Billing.Models.Responses;
using Bit.Core;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace Bit.Api.Billing.Controllers;

[Route("providers/{providerId:guid}/billing")]
[Authorize("Application")]
public class ProviderBillingController(
    ICurrentContext currentContext,
    IFeatureService featureService,
    IProviderBillingService providerBillingService,
    IProviderRepository providerRepository,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService) : Controller
{
    [HttpGet("payment-information")]
    public async Task<IResult> GetPaymentInformationAsync([FromRoute] Guid providerId)
    {
        var (provider, result) = await GetAuthorizedBillableProviderOrResultAsync(providerId);

        if (provider == null)
        {
            return result;
        }

        var paymentInformation = await subscriberService.GetPaymentInformation(provider);

        if (paymentInformation == null)
        {
            return TypedResults.NotFound();
        }

        var response = PaymentInformationResponse.From(paymentInformation);

        return TypedResults.Ok(response);
    }

    [HttpGet("payment-method")]
    public async Task<IResult> GetPaymentMethodAsync([FromRoute] Guid providerId)
    {
        var (provider, result) = await GetAuthorizedBillableProviderOrResultAsync(providerId);

        if (provider == null)
        {
            return result;
        }

        var maskedPaymentMethod = await subscriberService.GetPaymentMethod(provider);

        if (maskedPaymentMethod == null)
        {
            return TypedResults.NotFound();
        }

        var response = MaskedPaymentMethodResponse.From(maskedPaymentMethod);

        return TypedResults.Ok(response);
    }

    [HttpPut("payment-method")]
    public async Task<IResult> UpdatePaymentMethodAsync(
        [FromRoute] Guid providerId,
        [FromBody] TokenizedPaymentMethodRequestBody requestBody)
    {
        var (provider, result) = await GetAuthorizedBillableProviderOrResultAsync(providerId);

        if (provider == null)
        {
            return result;
        }

        var tokenizedPaymentMethod = new TokenizedPaymentMethodDTO(
            requestBody.Type,
            requestBody.Token);

        await subscriberService.UpdatePaymentMethod(provider, tokenizedPaymentMethod);

        // TODO: Do we need to try and pay the outstanding invoices here?
        await stripeAdapter.SubscriptionUpdateAsync(provider.GatewaySubscriptionId,
            new SubscriptionUpdateOptions
            {
                CollectionMethod = StripeConstants.CollectionMethod.ChargeAutomatically
            });

        return TypedResults.Ok();
    }

    [HttpPost]
    [Route("payment-method/verify-bank-account")]
    public async Task<IResult> VerifyBankAccountAsync(
        [FromRoute] Guid providerId,
        [FromBody] VerifyBankAccountRequestBody requestBody)
    {
        var (provider, result) = await GetAuthorizedBillableProviderOrResultAsync(providerId);

        if (provider == null)
        {
            return result;
        }

        await subscriberService.VerifyBankAccount(provider, (requestBody.Amount1, requestBody.Amount2));

        return TypedResults.Ok();
    }

    [HttpGet("subscription")]
    public async Task<IResult> GetSubscriptionAsync([FromRoute] Guid providerId)
    {
        var (provider, result) = await GetAuthorizedBillableProviderOrResultAsync(providerId);

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

    [HttpGet("tax-information")]
    public async Task<IResult> GetTaxInformationAsync([FromRoute] Guid providerId)
    {
        var (provider, result) = await GetAuthorizedBillableProviderOrResultAsync(providerId);

        if (provider == null)
        {
            return result;
        }

        var taxInformation = await subscriberService.GetTaxInformation(provider);

        if (taxInformation == null)
        {
            return TypedResults.NotFound();
        }

        var response = TaxInformationResponse.From(taxInformation);

        return TypedResults.Ok(response);
    }

    [HttpPut("tax-information")]
    public async Task<IResult> UpdateTaxInformationAsync(
        [FromRoute] Guid providerId,
        [FromBody] TaxInformationRequestBody requestBody)
    {
        var (provider, result) = await GetAuthorizedBillableProviderOrResultAsync(providerId);

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

    private async Task<(Provider, IResult)> GetAuthorizedBillableProviderOrResultAsync(Guid providerId)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling))
        {
            return (null, TypedResults.NotFound());
        }

        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            return (null, TypedResults.NotFound());
        }

        if (!currentContext.ProviderProviderAdmin(providerId))
        {
            return (null, TypedResults.Unauthorized());
        }

        if (!provider.IsBillable())
        {
            return (null, TypedResults.Unauthorized());
        }

        return (provider, null);
    }
}
