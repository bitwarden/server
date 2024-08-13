using Bit.Core;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Extensions;
using Bit.Core.Context;
using Bit.Core.Models.Api;
using Bit.Core.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

public abstract class BaseProviderController(
    ICurrentContext currentContext,
    IFeatureService featureService,
    ILogger<BaseProviderController> logger,
    IProviderRepository providerRepository,
    IUserService userService) : Controller
{
    protected readonly IUserService UserService = userService;

    protected static NotFound<ErrorResponseModel> NotFoundResponse() =>
        TypedResults.NotFound(new ErrorResponseModel("Resource not found."));

    protected static JsonHttpResult<ErrorResponseModel> ServerErrorResponse(string errorMessage) =>
        TypedResults.Json(
            new ErrorResponseModel(errorMessage),
            statusCode: StatusCodes.Status500InternalServerError);

    protected static JsonHttpResult<ErrorResponseModel> UnauthorizedResponse() =>
        TypedResults.Json(
            new ErrorResponseModel("Unauthorized."),
            statusCode: StatusCodes.Status401Unauthorized);

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
            logger.LogError(
                "Cannot run Consolidated Billing operation for provider ({ProviderID}) while feature flag is disabled",
                providerId);

            return (null, NotFoundResponse());
        }

        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            logger.LogError(
                "Cannot find provider ({ProviderID}) for Consolidated Billing operation",
                providerId);

            return (null, NotFoundResponse());
        }

        if (!checkAuthorization(providerId))
        {
            var user = await UserService.GetUserByPrincipalAsync(User);

            logger.LogError(
                "User ({UserID}) is not authorized to perform Consolidated Billing operation for provider ({ProviderID})",
                user?.Id, providerId);

            return (null, UnauthorizedResponse());
        }

        if (!provider.IsBillable())
        {
            logger.LogError(
                "Cannot run Consolidated Billing operation for provider ({ProviderID}) that is not billable",
                providerId);

            return (null, UnauthorizedResponse());
        }

        if (provider.IsStripeEnabled())
        {
            return (provider, null);
        }

        logger.LogError(
            "Cannot run Consolidated Billing operation for provider ({ProviderID}) that is missing Stripe configuration",
            providerId);

        return (null, ServerErrorResponse("Something went wrong with your request. Please contact support."));
    }
}
