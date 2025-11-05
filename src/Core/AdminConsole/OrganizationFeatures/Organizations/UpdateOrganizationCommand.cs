using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Services;
using Stripe;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

/// <summary>
/// Request model for updating an organization.
/// </summary>
public record UpdateOrganizationRequest
{
    /// <summary>
    /// The organization to update. This should be unchanged - any properties to be updated should be
    /// separate properties on this request model.
    /// </summary>
    public required Organization Organization { get; init; }

    /// <summary>
    /// The new organization name to apply.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The new business name to apply.
    /// </summary>
    public string? BusinessName { get; init; }

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
    IOrganizationService organizationService
) : IUpdateOrganizationCommand
{
    public async Task UpdateAsync(UpdateOrganizationRequest request)
    {
        var organization = request.Organization;

        // Store original values for comparison
        var originalName = organization.Name;
        var originalBillingEmail = organization.BillingEmail;

        // Apply updates to organization
        await ApplyUpdatesToOrganizationAsync(request);

        await organizationService.ReplaceAndUpdateCacheAsync(organization, EventType.Organization_Updated);

        // Update billing information in Stripe if required
        await UpdateBillingIfRequiredAsync(organization, originalName, originalBillingEmail);
    }

    private async Task ApplyUpdatesToOrganizationAsync(UpdateOrganizationRequest request)
    {
        var organization = request.Organization;
        organization.Name = request.Name;
        organization.BusinessName = request.BusinessName;

        // Only update billing email if NOT managed by a provider
        var provider = await providerRepository.GetByOrganizationIdAsync(organization.Id);
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
