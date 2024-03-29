using Bit.Api.Billing.Models;
using Bit.Core;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Commands;
using Bit.Core.Context;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Billing.Controllers;

[Route("providers/{providerId:guid}/organizations")]
public class ProviderOrganizationController(
    IAssignSeatsToClientOrganizationCommand assignSeatsToClientOrganizationCommand,
    ICurrentContext currentContext,
    IFeatureService featureService,
    ILogger<ProviderOrganizationController> logger,
    IOrganizationRepository organizationRepository,
    IProviderRepository providerRepository,
    IProviderOrganizationRepository providerOrganizationRepository) : Controller
{
    [HttpPut("{providerOrganizationId:guid}")]
    public async Task<IResult> UpdateAsync(
        [FromRoute] Guid providerId,
        [FromRoute] Guid providerOrganizationId,
        [FromBody] UpdateProviderOrganizationRequestBody requestBody)
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

        var organization = await organizationRepository.GetByIdAsync(providerOrganization.OrganizationId);

        if (organization == null)
        {
            logger.LogError("The organization ({OrganizationID}) represented by provider organization ({ProviderOrganizationID}) could not be found.", providerOrganization.OrganizationId, providerOrganization.Id);

            return TypedResults.Problem();
        }

        await assignSeatsToClientOrganizationCommand.AssignSeatsToClientOrganization(
            provider,
            organization,
            requestBody.AssignedSeats);

        return TypedResults.NoContent();
    }
}
