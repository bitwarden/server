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
    IUserService userService) : BaseAdminConsoleController
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
            return Error.Unauthorized();
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
            return Error.NotFound();
        }

        if (providerOrganization.ProviderId != provider.Id)
        {
            return Error.NotFound();
        }

        var clientOrganization = await organizationRepository.GetByIdAsync(providerOrganization.OrganizationId);

        if (clientOrganization is not { Status: OrganizationStatusType.Managed })
        {
            return Error.InternalError();
        }

        var seatAdjustment = requestBody.AssignedSeats - (clientOrganization.Seats ?? 0);

        var seatAdjustmentResultsInPurchase = await providerBillingService.SeatAdjustmentResultsInPurchase(
            provider,
            clientOrganization.PlanType,
            seatAdjustment);

        if (seatAdjustmentResultsInPurchase && !currentContext.ProviderProviderAdmin(provider.Id))
        {
            return TypedResults.Json(
                new ErrorResponseModel("Service users cannot purchase additional seats."),
                statusCode: StatusCodes.Status401Unauthorized);
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
            return Error.Unauthorized();
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
            return Error.Unauthorized();
        }

        var (provider, result) = await TryGetBillableProviderAsync(providerId);

        if (provider == null)
        {
            return result;
        }

        if (!await currentContext.OrganizationOwner(requestBody.OrganizationId))
        {
            return Error.Unauthorized();
        }

        var addableOrganizations = await organizationRepository.GetAddableToProviderByUserIdAsync(userId.Value, provider.Type);
        var organization = addableOrganizations.FirstOrDefault(o => o.Id == requestBody.OrganizationId);

        if (organization == null)
        {
            return Error.NotFound();
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

            return (null, Error.NotFound());
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

        return (null, Error.InternalError());
    }

}
