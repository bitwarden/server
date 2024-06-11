using Bit.Api.Billing.Models.Requests;
using Bit.Core;
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
    IFeatureService featureService,
    ILogger<ProviderClientsController> logger,
    IOrganizationRepository organizationRepository,
    IProviderBillingService providerBillingService,
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
            AdditionalSeats = requestBody.Seats,
            Owner = user,
            BillingEmail = provider.BillingEmail,
            OwnerKey = requestBody.Key,
            PublicKey = requestBody.KeyPair.PublicKey,
            PrivateKey = requestBody.KeyPair.EncryptedPrivateKey,
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

        if (clientOrganization.Seats != requestBody.AssignedSeats)
        {
            await providerBillingService.AssignSeatsToClientOrganization(
                provider,
                clientOrganization,
                requestBody.AssignedSeats);
        }

        clientOrganization.Name = requestBody.Name;

        await organizationRepository.ReplaceAsync(clientOrganization);

        return TypedResults.Ok();
    }
}
