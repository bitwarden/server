using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public static class OrganizationExtensions
{
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
}
