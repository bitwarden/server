using System.Text.Json;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Api.IntegrationTest.Auth.Helpers;

/// <summary>
/// Arrange helpers for SSO just-in-time (JIT) provisioning tests: the organization's SSO configuration and the
/// account state of a member that SSO has just provisioned.
/// </summary>
internal static class SsoJitProvisionTestHelpers
{
    /// <summary>
    /// Creates an enabled SSO configuration for the organization with the given member decryption type.
    /// </summary>
    public static Task<SsoConfig> CreateSsoConfigAsync(
        ISsoConfigRepository ssoConfigRepository, Guid organizationId, MemberDecryptionType memberDecryptionType) =>
        ssoConfigRepository.CreateAsync(new SsoConfig
        {
            OrganizationId = organizationId,
            Enabled = true,
            Data = JsonSerializer.Serialize(new SsoConfigurationData
            {
                MemberDecryptionType = memberDecryptionType,
            }, JsonHelpers.CamelCase),
        });

    /// <summary>
    /// Clears the master password, the user key, and the account keys of a registered account, so that the account
    /// matches a member that SSO has just provisioned.
    /// </summary>
    public static async Task ClearMasterPasswordAndKeysAsync(IUserRepository userRepository, User user)
    {
        user.MasterPassword = null;
        user.Key = null;
        user.PrivateKey = null;
        user.PublicKey = null;
        user.SignedPublicKey = null;
        await userRepository.ReplaceAsync(user);
    }
}
