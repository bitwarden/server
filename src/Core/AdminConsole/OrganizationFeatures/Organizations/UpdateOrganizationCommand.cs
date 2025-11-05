using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Stripe;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

/// <summary>
/// Request model for updating an organization.
/// </summary>
/// <param name="Organization">The organization to update.</param>
/// <param name="Name">The organization name.</param>
/// <param name="BusinessName">The business name.</param>
/// <param name="BillingEmail">The billing email address.</param>
/// <param name="PublicKey">The organization's public key (optional, only set if not already present).</param>
/// <param name="EncryptedPrivateKey">The organization's encrypted private key (optional, only set if not already present).</param>
public record UpdateOrganizationRequest(
    Organization Organization,
    string Name,
    string? BusinessName,
    string BillingEmail,
    string? PublicKey = null,
    string? EncryptedPrivateKey = null
);

public class UpdateOrganizationCommand(
    IProviderRepository providerRepository,
    IOrganizationRepository organizationRepository,
    IStripeAdapter stripeAdapter,
    IOrganizationService organizationService
) : IUpdateOrganizationCommand
{
    public async Task UpdateAsync(UpdateOrganizationRequest request)
    {
        var organization = request.Organization;

        // Store original values for comparison
        var originalName = organization.Name;
        var originalBillingEmail = organization.BillingEmail;

        // Check if organization is managed by a provider
        var provider = await providerRepository.GetByOrganizationIdAsync(organization.Id);

        // Apply updates to organization
        ApplyUpdatesToOrganization(organization, request, provider);

        if (!string.IsNullOrWhiteSpace(organization.Identifier))
        {
            var orgById = await organizationRepository.GetByIdentifierAsync(organization.Identifier);
            if (orgById != null && orgById.Id != organization.Id)
            {
                throw new BadRequestException("Identifier already in use by another organization.");
            }
        }

        await organizationService.ReplaceAndUpdateCacheAsync(organization, EventType.Organization_Updated);

        // Update billing information in Stripe if required
        await UpdateBillingIfRequiredAsync(organization, originalName, originalBillingEmail);
    }

    private static void ApplyUpdatesToOrganization(Organization organization, UpdateOrganizationRequest request, Provider? provider)
    {
        organization.Name = request.Name;
        organization.BusinessName = request.BusinessName;

        // Only update billing email if NOT managed by a provider
        if (provider == null)
        {
            organization.BillingEmail = request.BillingEmail?.ToLowerInvariant()?.Trim()!;
        }

        // Update keys if provided and not already set
        if (!string.IsNullOrWhiteSpace(request.PublicKey) && string.IsNullOrWhiteSpace(organization.PublicKey))
        {
            organization.PublicKey = request.PublicKey;
        }

        if (!string.IsNullOrWhiteSpace(request.EncryptedPrivateKey) && string.IsNullOrWhiteSpace(organization.PrivateKey))
        {
            organization.PrivateKey = request.EncryptedPrivateKey;
        }
    }

    private async Task UpdateBillingIfRequiredAsync(Organization organization, string originalName, string? originalBillingEmail)
    {
        // Update Stripe if name or billing email changed
        var shouldUpdateBilling = originalName != organization.Name ||
                                  originalBillingEmail != organization.BillingEmail;

        if (!shouldUpdateBilling || string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            return;
        }

        var newDisplayName = organization.DisplayName();

        await stripeAdapter.CustomerUpdateAsync(organization.GatewayCustomerId,
            new CustomerUpdateOptions
            {
                Email = organization.BillingEmail,
                Description = organization.DisplayBusinessName(),
                InvoiceSettings = new CustomerInvoiceSettingsOptions
                {
                    // This overwrites the existing custom fields for this organization
                    CustomFields = [
                        new CustomerInvoiceSettingsCustomFieldOptions
                        {
                            Name = organization.SubscriberType(),
                            Value = newDisplayName.Length <= 30
                                ? newDisplayName
                                : newDisplayName[..30]
                        }]
                },
            });
    }
}
