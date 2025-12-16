using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Update;

public static class OrganizationUpdateExtensions
{
    /// <summary>
    /// Updates the organization name and/or billing email.
    /// Any null property on the request object will be skipped.
    /// </summary>
    public static void UpdateDetails(this Organization organization, OrganizationUpdateRequest request)
    {
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

    /// <summary>
    /// Updates the organization public and private keys if provided and not already set.
    /// This is legacy code for old organizations that were not created with a public/private keypair. It is a soft
    /// migration that will silently migrate organizations when they change their details or upgrade their plan.
    /// </summary>
    public static void BackfillPublicPrivateKeys(this Organization organization, OrganizationKeyPair? keyPair)
    {
        if (keyPair == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(keyPair.PublicKey) && string.IsNullOrWhiteSpace(organization.PublicKey))
        {
            organization.PublicKey = keyPair.PublicKey;
        }

        if (!string.IsNullOrWhiteSpace(keyPair.PrivateKey) && string.IsNullOrWhiteSpace(organization.PrivateKey))
        {
            organization.PrivateKey = keyPair.PrivateKey;
        }
    }

    /// <summary>
    /// Updates the organization public and private keys if provided and not already set.
    /// This is legacy code for old organizations that were not created with a public/private keypair. It is a soft
    /// migration that will silently migrate organizations when they change their details.
    /// </summary>
    public static void BackfillPublicPrivateKeys(this Organization organization, OrganizationUpdateRequest request)
    {
        organization.BackfillPublicPrivateKeys(request.Keys);
    }
}
