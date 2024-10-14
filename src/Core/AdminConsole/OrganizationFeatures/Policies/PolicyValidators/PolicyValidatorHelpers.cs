using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public static class PolicyValidatorHelpers
{
    /// <summary>
    /// Validate that a policy can be disabled when certain Member Decryption Options require the policy to be enabled.
    /// </summary>
    /// <param name="policyUpdate">The policy update to disable the policy.</param>
    /// <param name="decryptionOptions">The Member Decryption Options that require the policy to be enabled.</param>
    /// <returns>A validation error if validation was unsuccessful, otherwise an empty string</returns>
    public static async Task<string> ValidateDecryptionTypesNotEnabledAsync(PolicyUpdate policyUpdate,
        MemberDecryptionType[] decryptionOptions, ISsoConfigRepository ssoConfigRepository)
        {
            var ssoConfig = await ssoConfigRepository.GetByOrganizationIdAsync(policyUpdate.OrganizationId);
            return ssoConfig?.GetData().MemberDecryptionType switch
            {
                MemberDecryptionType.KeyConnector when decryptionOptions.Contains(MemberDecryptionType.KeyConnector)
                    => "Key Connector is enabled and requires this policy.",
                MemberDecryptionType.TrustedDeviceEncryption when decryptionOptions.Contains(MemberDecryptionType
                    .TrustedDeviceEncryption) => "Trusted device encryption is on and requires this policy.",
                _ => ""
            };
        }
}
