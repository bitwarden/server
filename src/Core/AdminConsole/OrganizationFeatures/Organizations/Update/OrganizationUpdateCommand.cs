using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Update;

public class OrganizationUpdateCommand(
    IOrganizationService organizationService,
    IOrganizationRepository organizationRepository,
    IGlobalSettings globalSettings,
    IOrganizationBillingService organizationBillingService
) : IOrganizationUpdateCommand
{
    public async Task<Organization> UpdateAsync(OrganizationUpdateRequest request)
    {
        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (globalSettings.SelfHosted)
        {
            return await UpdateSelfHostedAsync(organization, request);
        }

        return await UpdateCloudAsync(organization, request);
    }

    private async Task<Organization> UpdateCloudAsync(Organization organization, OrganizationUpdateRequest request)
    {
        // Store original values for comparison
        var originalName = organization.Name;
        var originalBillingEmail = organization.BillingEmail;

        // Apply updates to organization
        organization.UpdateDetails(request);
        organization.BackfillPublicPrivateKeys(request);
        await organizationService.ReplaceAndUpdateCacheAsync(organization, EventType.Organization_Updated);

        // Update billing information in Stripe if required
        await UpdateBillingAsync(organization, originalName, originalBillingEmail);

        return organization;
    }

    /// <summary>
    /// Self-host cannot update the organization details because they are set by the license file.
    /// However, this command does offer a soft migration pathway for organizations without public and private keys.
    /// If we remove this migration code in the future, this command and endpoint can become cloud only.
    /// </summary>
    private async Task<Organization> UpdateSelfHostedAsync(Organization organization, OrganizationUpdateRequest request)
    {
        organization.BackfillPublicPrivateKeys(request);
        await organizationService.ReplaceAndUpdateCacheAsync(organization, EventType.Organization_Updated);
        return organization;
    }

    private async Task UpdateBillingAsync(Organization organization, string originalName, string? originalBillingEmail)
    {
        // Update Stripe if name or billing email changed
        var shouldUpdateBilling = originalName != organization.Name ||
                                  originalBillingEmail != organization.BillingEmail;

        if (!shouldUpdateBilling || string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            return;
        }

        await organizationBillingService.UpdateOrganizationNameAndEmail(organization);
    }
}
