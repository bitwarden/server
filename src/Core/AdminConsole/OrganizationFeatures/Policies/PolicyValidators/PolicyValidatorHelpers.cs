#nullable enable

using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public static class PolicyValidatorHelpers
{
    /// <summary>
    /// Validate that given Member Decryption Options are not enabled.
    /// Used for validation when disabling a policy that is required by certain Member Decryption Options.
    /// </summary>
    /// <param name="decryptionOptions">The Member Decryption Options that require the policy to be enabled.</param>
    /// <returns>A validation error if validation was unsuccessful, otherwise an empty string</returns>
    public static string ValidateDecryptionOptionsNotEnabled(this SsoConfig? ssoConfig,
        MemberDecryptionType[] decryptionOptions)
    {
        if (ssoConfig is not { Enabled: true })
        {
            return "";
        }

        return ssoConfig.GetData().MemberDecryptionType switch
        {
            MemberDecryptionType.KeyConnector when decryptionOptions.Contains(MemberDecryptionType.KeyConnector)
                => "Key Connector is enabled and requires this policy.",
            MemberDecryptionType.TrustedDeviceEncryption when decryptionOptions.Contains(MemberDecryptionType
                .TrustedDeviceEncryption) => "Trusted device encryption is on and requires this policy.",
            _ => ""
        };
    }
}
