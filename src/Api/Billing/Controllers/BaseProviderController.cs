using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Extensions;
using Bit.Core.Context;
using Bit.Core.Services;

namespace Bit.Api.Billing.Controllers;

public abstract class BaseProviderController(
    ICurrentContext currentContext,
    ILogger<BaseProviderController> logger,
    IProviderRepository providerRepository,
    IUserService userService) : BaseBillingController
{
    protected readonly IUserService UserService = userService;

    protected Task<(Provider, IResult)> TryGetBillableProviderForAdminOperation(
        Guid providerId) => TryGetBillableProviderAsync(providerId, currentContext.ProviderProviderAdmin);

    protected Task<(Provider, IResult)> TryGetBillableProviderForServiceUserOperation(
        Guid providerId) => TryGetBillableProviderAsync(providerId, currentContext.ProviderUser);

    private async Task<(Provider, IResult)> TryGetBillableProviderAsync(
        Guid providerId,
        Func<Guid, bool> checkAuthorization)
    {
        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            logger.LogError(
                "Cannot find provider ({ProviderID}) for Consolidated Billing operation",
                providerId);

            return (null, Error.NotFound());
        }

        if (!checkAuthorization(providerId))
        {
            var user = await UserService.GetUserByPrincipalAsync(User);

            logger.LogError(
                "User ({UserID}) is not authorized to perform Consolidated Billing operation for provider ({ProviderID})",
                user?.Id, providerId);

            return (null, Error.Unauthorized());
        }

        if (!provider.IsBillable())
        {
            logger.LogError(
                "Cannot run Consolidated Billing operation for provider ({ProviderID}) that is not billable",
                providerId);

            return (null, Error.Unauthorized());
        }

        if (provider.IsStripeEnabled())
        {
            return (provider, null);
        }

        logger.LogError(
            "Cannot run Consolidated Billing operation for provider ({ProviderID}) that is missing Stripe configuration",
            providerId);

        return (null, Error.ServerError());
    }
}
