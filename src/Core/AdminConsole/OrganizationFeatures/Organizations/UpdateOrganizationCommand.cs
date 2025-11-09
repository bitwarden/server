using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

/// <summary>
/// Request model for updating the name, billing email, and/or private keys for an organization (legacy migration code).
/// Any combination of these properties can be updated, so they are optional. If none are specified it will not update anything.
/// </summary>
public record UpdateOrganizationRequest
{
    /// <summary>
    /// The ID of the organization to update.
    /// </summary>
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// The new organization name to apply (optional, this is skipped if not provided).
    /// </summary>
    public required string? Name { get; init; }

    /// <summary>
    /// The new billing email address to apply (optional, this is skipped if not provided).
    /// </summary>
    public string? BillingEmail { get; init; }

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
    IOrganizationService organizationService,
    IOrganizationRepository organizationRepository,
    IGlobalSettings globalSettings,
    IOrganizationBillingService organizationBillingService
) : IUpdateOrganizationCommand
{
    public async Task<Organization> UpdateAsync(UpdateOrganizationRequest request)
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
        UpdateOrganizationDetails(organization, request);
        UpdatePublicPrivateKeyPair(organization, request);

        await organizationService.ReplaceAndUpdateCacheAsync(organization, EventType.Organization_Updated);

        // Update billing information in Stripe if required
        await UpdateBillingIfRequiredAsync(organization, originalName, originalBillingEmail);

        return organization;
    }

    private void UpdateOrganizationDetails(Organization organization, UpdateOrganizationRequest request)
    {
        if (globalSettings.SelfHosted)
        {
            // These values come from the license file and cannot be updated on self-hosted instances.
            // The only thing they can actually update here is to backfill their public/private keypair if missing
            // (for old organizations).
            return;
        }

        // These values may or may not be sent by the client depending on the operation being performed.
        // Skip any values not provided.
        if (request.Name is not null)
        {
            organization.Name = request.Name;
        }

        if (request.BillingEmail is not null)
        {
            organization.BillingEmail = request.BillingEmail.ToLowerInvariant().Trim();
        }
    }

    private void UpdatePublicPrivateKeyPair(Organization organization, UpdateOrganizationRequest request)
    {
        // Update keys if provided and not already set.
        // This is legacy code for old organizations that were not created with a public/private keypair.
        // FIXME: check if we can remove this migration pathway as it is now years old.
        // This is the only part of the command that does anything on self-host, so if this is removed then
        // this endpoint can be blocked for self-host entirely.
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

        await organizationBillingService.UpdateOrganizationNameAndEmail(organization);
    }
}
