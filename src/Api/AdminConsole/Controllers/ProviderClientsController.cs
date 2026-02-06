// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Billing.Controllers;
using Bit.Api.Billing.Models.Requests;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

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
    private readonly ICurrentContext _currentContext = currentContext;

    [HttpPost]
    [SelfHosted(NotSelfHostedOnly = true)]
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

        if (seatAdjustmentResultsInPurchase && !_currentContext.ProviderProviderAdmin(provider.Id))
        {
            return Error.Unauthorized("Service users cannot purchase additional seats.");
        }

        await providerBillingService.ScaleSeats(provider, clientOrganization.PlanType, seatAdjustment);

        clientOrganization.Name = requestBody.Name;
        clientOrganization.Seats = requestBody.AssignedSeats;

        await organizationRepository.ReplaceAsync(clientOrganization);

        return TypedResults.Ok();
    }

    [HttpGet("addable")]
    [SelfHosted(NotSelfHostedOnly = true)]
    public async Task<IResult> GetAddableOrganizationsAsync([FromRoute] Guid providerId)
    {
        var (provider, result) = await TryGetBillableProviderForServiceUserOperation(providerId);

        if (provider == null)
        {
            return result;
        }

        var userId = _currentContext.UserId;

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
    public async Task<IResult> AddExistingOrganizationAsync(
        [FromRoute] Guid providerId,
        [FromBody] AddExistingOrganizationRequestBody requestBody)
    {
        var (provider, result) = await TryGetBillableProviderForServiceUserOperation(providerId);

        if (provider == null)
        {
            return result;
        }

        var organization = await organizationRepository.GetByIdAsync(requestBody.OrganizationId);

        if (organization == null)
        {
            return Error.BadRequest("The organization being added to the provider does not exist.");
        }

        await providerBillingService.AddExistingOrganization(provider, organization, requestBody.Key);

        return TypedResults.Ok();
    }
}
