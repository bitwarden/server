using Bit.Api.Billing.Models.Requests;
using Bit.Api.Billing.Models.Responses;
using Bit.Core;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Extensions;
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

        throw new NotImplementedException();
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
