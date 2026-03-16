using Bit.Core.AdminConsole.Entities;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public static class OrganizationExtensions
{
    /// <summary>
    /// Updates the organization public and private keys if provided and not already set.
    /// This is legacy code for old organizations that were not created with a public/private keypair.
    /// It is a soft migration that will silently migrate organizations when they perform certain actions,
    /// e.g. change their details or upgrade their plan.
    /// </summary>
    public static void BackfillPublicPrivateKeys(this Organization organization, PublicKeyEncryptionKeyPairData? keyPair)
    {
        // Only backfill if both new keys are provided and both old keys are missing.
        if (string.IsNullOrWhiteSpace(keyPair?.PublicKey) ||
            string.IsNullOrWhiteSpace(keyPair.WrappedPrivateKey) ||
            !string.IsNullOrWhiteSpace(organization.PublicKey) ||
            !string.IsNullOrWhiteSpace(organization.PrivateKey))
        {
            return;
        }

        organization.PublicKey = keyPair.PublicKey;
        organization.PrivateKey = keyPair.WrappedPrivateKey;
    }
}
