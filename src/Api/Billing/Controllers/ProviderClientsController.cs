using Bit.Api.Billing.Models.Requests;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("providers/{providerId:guid}/clients")]
public class ProviderClientsController(
    ICurrentContext currentContext,
    ILogger<BaseProviderController> logger,
    IOrganizationRepository organizationRepository,
    IProviderBillingService providerBillingService,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderRepository providerRepository,
    IProviderService providerService,
    IUserService userService) : BaseProviderController(currentContext, logger, providerRepository, userService)
{
    [HttpPost]
    public async Task<IResult> CreateAsync(
        [FromRoute] Guid providerId,
        [FromBody] CreateClientOrganizationRequestBody requestBody)
    {
        var (provider, result) = await TryGetBillableProviderForAdminOperation(providerId);

        if (provider == null)
        {
            return result;
        }

        var user = await UserService.GetUserByPrincipalAsync(User);

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
            PublicKey = requestBody.KeyPair.PublicKey,
            PrivateKey = requestBody.KeyPair.EncryptedPrivateKey,
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
    public async Task<IResult> UpdateAsync(
        [FromRoute] Guid providerId,
        [FromRoute] Guid providerOrganizationId,
        [FromBody] UpdateClientOrganizationRequestBody requestBody)
    {
        var (provider, result) = await TryGetBillableProviderForServiceUserOperation(providerId);

        if (provider == null)
        {
            return result;
        }

        var providerOrganization = await providerOrganizationRepository.GetByIdAsync(providerOrganizationId);

        if (providerOrganization == null)
        {
            return Error.NotFound();
        }

        var clientOrganization = await organizationRepository.GetByIdAsync(providerOrganization.OrganizationId);

        if (clientOrganization is not { Status: OrganizationStatusType.Managed })
        {
            return Error.ServerError();
        }

        var seatAdjustment = requestBody.AssignedSeats - (clientOrganization.Seats ?? 0);

        var seatAdjustmentResultsInPurchase = await providerBillingService.SeatAdjustmentResultsInPurchase(
            provider,
            clientOrganization.PlanType,
            seatAdjustment);

        if (seatAdjustmentResultsInPurchase && !currentContext.ProviderProviderAdmin(provider.Id))
        {
            return Error.Unauthorized("Service users cannot purchase additional seats.");
        }

        await providerBillingService.ScaleSeats(provider, clientOrganization.PlanType, seatAdjustment);

        clientOrganization.Name = requestBody.Name;
        clientOrganization.Seats = requestBody.AssignedSeats;

        await organizationRepository.ReplaceAsync(clientOrganization);

        return TypedResults.Ok();
    }
}
