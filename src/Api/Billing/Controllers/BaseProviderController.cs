using Bit.Core;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Extensions;
using Bit.Core.Context;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

public abstract class BaseProviderController(
    ICurrentContext currentContext,
    IFeatureService featureService,
    IProviderRepository providerRepository) : Controller
{
    protected Task<(Provider, IResult)> TryGetBillableProviderForAdminOperation(
        Guid providerId) => TryGetBillableProviderAsync(providerId, currentContext.ProviderProviderAdmin);

    protected Task<(Provider, IResult)> TryGetBillableProviderForServiceUserOperation(
        Guid providerId) => TryGetBillableProviderAsync(providerId, currentContext.ProviderUser);

    private async Task<(Provider, IResult)> TryGetBillableProviderAsync(
        Guid providerId,
        Func<Guid, bool> checkAuthorization)
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

        if (!checkAuthorization(providerId))
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
