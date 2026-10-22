using System.ComponentModel.DataAnnotations;
using Bit.Api.Models.Request;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.UserFeatures.DeviceTrust.Data;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.SsoJitProvisionFinish;

/// <summary>
/// Combines the bodies of <c>POST accounts/keys</c>, <c>PUT organizations/{orgId}/users/{userId}/reset-password-enrollment</c>
/// and <c>PUT devices/{identifier}/keys</c> for a trusted device encryption SSO JIT user.
/// </summary>
public class SsoJitProvisionFinishTrustedDeviceRequestModel : IValidatableObject
{
    /// <summary>
    /// The body of <c>POST accounts/keys</c>. MVC validates nested models, so its attributes still run.
    /// </summary>
    [Required]
    public required KeysRequestModel Keys { get; set; }

    public required Guid OrganizationId { get; set; }

    /// <summary>
    /// The user key encrypted with the organization public key. Required when
    /// <see cref="AcceptOrganizationInvite"/> is true; ignored otherwise.
    /// For now, this is tied to <see cref="AcceptOrganizationInvite"/>.
    /// </summary>
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? ResetPasswordKey { get; set; }

    /// <summary>
    /// The body of <c>PUT devices/{identifier}/keys</c> for the device in the access token.
    /// Omit when the user did not choose to trust the device.
    /// </summary>
    public DeviceKeysRequestModel? DeviceKeys { get; set; }

    /// <summary>
    /// True moves the pending organization membership to Accepted and enrolls in account recovery. False leaves
    /// it Invited (open-invite links). The C# <c>required</c> modifier makes System.Text.Json reject a body without it.
    /// For now, account recovery enrollment is tied to this flag.
    /// </summary>
    public required bool AcceptOrganizationInvite { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OrganizationId == Guid.Empty)
        {
            yield return new ValidationResult($"{nameof(OrganizationId)} must be supplied.", [nameof(OrganizationId)]);
        }

        if (Keys.AccountKeys != null && !IsV2Encryption(Keys))
        {
            yield return new ValidationResult(
                $"{nameof(Keys.AccountKeys)} are only supported for V2 encryption.", [nameof(Keys)]);
        }

        // For now, account recovery enrollment follows AcceptOrganizationInvite.
        if (AcceptOrganizationInvite && !OrganizationUser.IsValidResetPasswordKey(ResetPasswordKey))
        {
            yield return new ValidationResult(
                $"{nameof(ResetPasswordKey)} must be supplied when {nameof(AcceptOrganizationInvite)} is true.",
                [nameof(ResetPasswordKey)]);
        }
    }

    /// <param name="currentDeviceIdentifier">The device identifier from the access token.</param>
    public FinishSsoJitProvisionTrustedDeviceData ToData(string? currentDeviceIdentifier)
    {
        return new FinishSsoJitProvisionTrustedDeviceData
        {
            AccountKeys = Keys.AccountKeys?.ToAccountKeysData() ?? ToLegacyAccountKeysData(),
            UserKeyId = Keys.AccountKeys == null ? null : KeyId.FromHexEncodedString(Keys.UserKeyId),
            OrganizationId = OrganizationId,
            ResetPasswordKey = ResetPasswordKey,
            DeviceKeys = DeviceKeys == null ? null : ToTrustedDeviceKeysData(DeviceKeys, currentDeviceIdentifier)
        };
    }

    private static bool IsV2Encryption(KeysRequestModel keys)
    {
        try
        {
            return keys.AccountKeys.ToAccountKeysData().IsV2Encryption();
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }

    // TODO: Drop together with the legacy KeysRequestModel fields. https://bitwarden.atlassian.net/browse/PM-27329
    private UserAccountKeysData ToLegacyAccountKeysData()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return new UserAccountKeysData
        {
            PublicKeyEncryptionKeyPairData = new PublicKeyEncryptionKeyPairData(Keys.EncryptedPrivateKey, Keys.PublicKey)
        };
#pragma warning restore CS0618
    }

    private static TrustedDeviceKeysData ToTrustedDeviceKeysData(
        DeviceKeysRequestModel deviceKeys, string? currentDeviceIdentifier)
    {
        if (string.IsNullOrWhiteSpace(currentDeviceIdentifier))
        {
            throw new BadRequestException("A device identifier is required to trust the current device.");
        }

        return new TrustedDeviceKeysData
        {
            DeviceIdentifier = currentDeviceIdentifier,
            EncryptedUserKey = deviceKeys.EncryptedUserKey,
            EncryptedPublicKey = deviceKeys.EncryptedPublicKey,
            EncryptedPrivateKey = deviceKeys.EncryptedPrivateKey
        };
    }
}
