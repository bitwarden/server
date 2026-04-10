// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Providers.Requirements;
using Bit.Api.Billing.Models.Requests;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("providers/{providerId:guid}/clients")]
[Authorize("Application")]
public class ProviderClientsController(
    ICurrentContext currentContext,
    ILogger<ProviderClientsController> logger,
    IOrganizationRepository organizationRepository,
    IProviderBillingService providerBillingService,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderRepository providerRepository,
    IProviderService providerService,
    IUserService userService) : Controller
{
    [HttpPost]
    [SelfHosted(NotSelfHostedOnly = true)]
    [Authorize<ProviderAdminRequirement>]
    public async Task<IResult> CreateAsync(
        [FromRoute] Guid providerId,
        [FromBody] CreateClientOrganizationRequestBody requestBody)
    {
        var (provider, result) = await TryGetBillableProviderAsync(providerId);

        if (provider == null)
        {
            return result;
        }

        var user = await userService.GetUserByPrincipalAsync(User);

        if (user == null)
        {
            return Error401();
        }

        var organizationSignup = new OrganizationSignup
        {
            Name = requestBody.Name,
            Plan = requestBody.PlanType,
            AdditionalSeats = requestBody.Seats,
            Owner = user,
            BillingEmail = provider.BillingEmail,
            OwnerKey = requestBody.Key,
            Keys = requestBody.KeyPair.ToPublicKeyEncryptionKeyPairData(),
            CollectionName = requestBody.CollectionName,
            IsFromProvider = true
        };

        var providerOrganization = await providerService.CreateOrganizationAsync(
            providerId,
            organizationSignup,
            requestBody.OwnerEmail,
            user);

        var clientOrganization = await organizationRepository.GetByIdAsync(providerOrganization.OrganizationId);

        await providerBillingService.ScaleSeats(
            provider,
            requestBody.PlanType,
            requestBody.Seats);

        await providerBillingService.CreateCustomerForClientOrganization(
            provider,
            clientOrganization);

        clientOrganization.Status = OrganizationStatusType.Managed;

        await organizationRepository.ReplaceAsync(clientOrganization);

        return TypedResults.Ok();
    }

    [HttpPut("{providerOrganizationId:guid}")]
    [SelfHosted(NotSelfHostedOnly = true)]
    [Authorize<ProviderUserRequirement>]
    public async Task<IResult> UpdateAsync(
        [FromRoute] Guid providerId,
        [FromRoute] Guid providerOrganizationId,
        [FromBody] UpdateClientOrganizationRequestBody requestBody)
    {
        var (provider, result) = await TryGetBillableProviderAsync(providerId);

        if (provider == null)
        {
            return result;
        }

        var providerOrganization = await providerOrganizationRepository.GetByIdAsync(providerOrganizationId);

        if (providerOrganization == null)
        {
            return Error404();
        }

        if (providerOrganization.ProviderId != provider.Id)
        {
            return Error404();
        }

        var clientOrganization = await organizationRepository.GetByIdAsync(providerOrganization.OrganizationId);

        if (clientOrganization is not { Status: OrganizationStatusType.Managed })
        {
            return Error500();
        }

        var seatAdjustment = requestBody.AssignedSeats - (clientOrganization.Seats ?? 0);

        var seatAdjustmentResultsInPurchase = await providerBillingService.SeatAdjustmentResultsInPurchase(
            provider,
            clientOrganization.PlanType,
            seatAdjustment);

        if (seatAdjustmentResultsInPurchase && !currentContext.ProviderProviderAdmin(provider.Id))
        {
            return Error401("Service users cannot purchase additional seats.");
        }

        await providerBillingService.ScaleSeats(provider, clientOrganization.PlanType, seatAdjustment);

        clientOrganization.Name = requestBody.Name;
        clientOrganization.Seats = requestBody.AssignedSeats;

        await organizationRepository.ReplaceAsync(clientOrganization);

        return TypedResults.Ok();
    }

    [HttpGet("addable")]
    [SelfHosted(NotSelfHostedOnly = true)]
    [Authorize<ProviderUserRequirement>]
    public async Task<IResult> GetAddableOrganizationsAsync([FromRoute] Guid providerId)
    {
        var (provider, result) = await TryGetBillableProviderAsync(providerId);

        if (provider == null)
        {
            return result;
        }

        var userId = currentContext.UserId;

        if (!userId.HasValue)
        {
            return Error401();
        }

        var addable =
            await providerBillingService.GetAddableOrganizations(provider, userId.Value);

        return TypedResults.Ok(addable);
    }

    [HttpPost("existing")]
    [SelfHosted(NotSelfHostedOnly = true)]
    [Authorize<ProviderAdminRequirement>]
    public async Task<IResult> AddExistingOrganizationAsync(
        [FromRoute] Guid providerId,
        [FromBody] AddExistingOrganizationRequestBody requestBody)
    {
        var userId = currentContext.UserId;
        if (!userId.HasValue)
        {
            return Error401();
        }

        var (provider, result) = await TryGetBillableProviderAsync(providerId);

        if (provider == null)
        {
            return result;
        }

        if (!await currentContext.OrganizationOwner(requestBody.OrganizationId))
        {
            return Error401();
        }

        var addableOrganizations = await organizationRepository.GetAddableToProviderByUserIdAsync(userId.Value, provider.Type);
        var organization = addableOrganizations.FirstOrDefault(o => o.Id == requestBody.OrganizationId);

        if (organization == null)
        {
            return Error404();
        }

        await providerBillingService.AddExistingOrganization(provider, organization, requestBody.Key);

        return TypedResults.Ok();
    }

    private async Task<(Provider, IResult)> TryGetBillableProviderAsync(Guid providerId)
    {
        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            logger.LogError(
                "Cannot find provider ({ProviderID}) for Consolidated Billing operation",
                providerId);

            return (null, Error404());
        }

        if (!provider.IsBillable())
        {
            logger.LogError(
                "Cannot run Consolidated Billing operation for provider ({ProviderID}) that is not billable",
                providerId);

            return (null, Error401());
        }

        if (provider.IsStripeEnabled())
        {
            return (provider, null);
        }

        logger.LogError(
            "Cannot run Consolidated Billing operation for provider ({ProviderID}) that is missing Stripe configuration",
            providerId);

        return (null, Error500());
    }

    private static IResult Error404() =>
        TypedResults.NotFound(new ErrorResponseModel("Resource not found."));

    private static IResult Error401(string message = "Unauthorized.") =>
        TypedResults.Json(
            new ErrorResponseModel(message),
            statusCode: StatusCodes.Status401Unauthorized);

    private static IResult Error500() =>
        TypedResults.Json(
            new ErrorResponseModel("Something went wrong with your request. Please contact support for assistance."),
            statusCode: StatusCodes.Status500InternalServerError);
}
