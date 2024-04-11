using Bit.Api.Billing.Models.Requests;
using Bit.Core;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Commands;
using Bit.Core.Context;
using Bit.Core.Models.Business;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("providers/{providerId:guid}/clients")]
public class ProviderClientsController(
    IAssignSeatsToClientOrganizationCommand assignSeatsToClientOrganizationCommand,
    ICreateCustomerCommand createCustomerCommand,
    ICurrentContext currentContext,
    IFeatureService featureService,
    ILogger<ProviderClientsController> logger,
    IOrganizationRepository organizationRepository,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderRepository providerRepository,
    IProviderService providerService,
    IUserService userService) : Controller
{
    [HttpPost]
    public async Task<IResult> CreateAsync(
        [FromRoute] Guid providerId,
        [FromBody] CreateClientOrganizationRequestBody requestBody)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling))
        {
            return TypedResults.NotFound();
        }

        var user = await userService.GetUserByPrincipalAsync(User);

        if (user == null)
        {
            return TypedResults.Unauthorized();
        }

        if (!currentContext.ManageProviderOrganizations(providerId))
        {
            return TypedResults.Unauthorized();
        }

        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            return TypedResults.NotFound();
        }

        var organizationSignup = new OrganizationSignup
        {
            Name = requestBody.Name,
            Plan = requestBody.PlanType,
            Owner = user,
            OwnerKey = requestBody.UserKey,
            PublicKey = requestBody.Keys.PublicKey,
            PrivateKey = requestBody.Keys.EncryptedPrivateKey,
            CollectionName = requestBody.CollectionName
        };

        var providerOrganization = await providerService.CreateOrganizationAsync(
            providerId,
            organizationSignup,
            requestBody.OwnerEmail,
            user);

        var clientOrganization = await organizationRepository.GetByIdAsync(providerOrganization.OrganizationId);

        if (clientOrganization == null)
        {
            logger.LogError("Newly created client organization ({ID}) could not be found", providerOrganization.OrganizationId);

            return TypedResults.Problem();
        }

        await assignSeatsToClientOrganizationCommand.AssignSeatsToClientOrganization(
            provider,
            clientOrganization,
            requestBody.Seats);

        await createCustomerCommand.CreateCustomer(
            provider,
            clientOrganization);

        return TypedResults.Ok();
    }

    [HttpPut("{providerOrganizationId:guid}")]
    public async Task<IResult> UpdateAsync(
        [FromRoute] Guid providerId,
        [FromRoute] Guid providerOrganizationId,
        [FromBody] UpdateClientOrganizationRequestBody requestBody)
    {
        if (!featureService.IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling))
        {
            return TypedResults.NotFound();
        }

        if (!currentContext.ProviderProviderAdmin(providerId))
        {
            return TypedResults.Unauthorized();
        }

        var provider = await providerRepository.GetByIdAsync(providerId);

        var providerOrganization = await providerOrganizationRepository.GetByIdAsync(providerOrganizationId);

        if (provider == null || providerOrganization == null)
        {
            return TypedResults.NotFound();
        }

        var clientOrganization = await organizationRepository.GetByIdAsync(providerOrganization.OrganizationId);

        if (clientOrganization == null)
        {
            logger.LogError("The client organization ({OrganizationID}) represented by provider organization ({ProviderOrganizationID}) could not be found.", providerOrganization.OrganizationId, providerOrganization.Id);

            return TypedResults.Problem();
        }

        await assignSeatsToClientOrganizationCommand.AssignSeatsToClientOrganization(
            provider,
            clientOrganization,
            requestBody.AssignedSeats);

        return TypedResults.Ok();
    }
}
