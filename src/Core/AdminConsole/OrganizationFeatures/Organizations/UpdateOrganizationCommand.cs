using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Stripe;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

/// <summary>
/// Request model for updating an organization.
/// </summary>
public record UpdateOrganizationRequest
{
    /// <summary>
    /// The ID of the organization to update.
    /// </summary>
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// The new organization name to apply.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The new billing email address to apply (ignored if organization is managed by a provider).
    /// </summary>
    public required string BillingEmail { get; init; }

    /// <summary>
    /// The organization's public key to set (optional, only set if not already present on the organization).
    /// </summary>
    public string? PublicKey { get; init; }

    /// <summary>
    /// The organization's encrypted private key to set (optional, only set if not already present on the organization).
    /// </summary>
    public string? EncryptedPrivateKey { get; init; }
}

public class UpdateOrganizationCommand(
    IProviderRepository providerRepository,
    IStripeAdapter stripeAdapter,
    IOrganizationService organizationService,
    IOrganizationRepository organizationRepository,
    IGlobalSettings globalSettings
) : IUpdateOrganizationCommand
{
    public async Task UpdateAsync(UpdateOrganizationRequest request)
    {
        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        // Store original values for comparison
        var originalName = organization.Name;
        var originalBillingEmail = organization.BillingEmail;

        // Apply updates to organization model
        await UpdateOrganizationDetailsAsync(organization, request);
        UpdatePublicPrivateKeyPairAsync(organization, request);

        await organizationService.ReplaceAndUpdateCacheAsync(organization, EventType.Organization_Updated);

        // Update billing information in Stripe if required
        await UpdateBillingIfRequiredAsync(organization, originalName, originalBillingEmail);
    }

    private async Task UpdateOrganizationDetailsAsync(Organization organization, UpdateOrganizationRequest request)
    {
        if (globalSettings.SelfHosted)
        {
            // These values come from the license file and cannot be updated on self-hosted instances.
            // The only thing they can actually update here is to backfill their public/private keypair if missing
            // (for old organizations).
            return;
        }

        organization.Name = request.Name;

        // Only update billing email if NOT managed by a provider
        var provider = await providerRepository.GetByOrganizationIdAsync(organization.Id);
        if (provider == null)
        {
            organization.BillingEmail = request.BillingEmail.ToLowerInvariant().Trim();
        }
    }

    private void UpdatePublicPrivateKeyPairAsync(Organization organization, UpdateOrganizationRequest request)
    {
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

        if (!shouldUpdateBilling || string.IsNullOrWhiteSpace(organization.GatewayCustomerId) || globalSettings.SelfHosted)
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
