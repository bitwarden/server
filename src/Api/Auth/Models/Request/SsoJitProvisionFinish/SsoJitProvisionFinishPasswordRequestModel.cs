using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.SsoJitProvisionFinish;

/// <summary>
/// Request to finish SSO JIT provisioning for a user in an organization that uses master password decryption.
/// Accepts only the modern MPAD + MPUD shape: unlike <c>accounts/set-password</c>, this endpoint has no legacy
/// clients to stay compatible with.
/// </summary>
public class SsoJitProvisionFinishPasswordRequestModel : IValidatableObject
{
    [Required]
    public required MasterPasswordAuthenticationDataRequestModel MasterPasswordAuthentication { get; set; }

    [Required]
    public required MasterPasswordUnlockDataRequestModel MasterPasswordUnlock { get; set; }

    public AccountKeysRequestModel? AccountKeys { get; set; }

    // TODO: legacy Keys can be removed once the EnableAccountEncryptionV2JitPasswordRegistration feature flag
    // has been unwound in https://bitwarden.atlassian.net/browse/PM-27327.
    [Obsolete("Use AccountKeys instead")]
    public KeysRequestModel? Keys { get; set; }

    [StringLength(50)]
    public string? MasterPasswordHint { get; set; }

    [Required]
    public required string OrgIdentifier { get; set; }

    /// <summary>
    /// Whether to accept the user's pending organization invite. Required so every caller decides explicitly;
    /// open-invite clients send <c>false</c> so the member stays Invited for later Admin Console checks.
    /// </summary>
    public required bool AcceptOrganizationInvite { get; set; }

    // TODO: Can be removed when its only consumer, SsoJitProvisionFinishController.FinishJitProvisionForPasswordV1Async(),
    // is removed with the V2 MP JIT feature flag.
    public User ToUser(User existingUser)
    {
        existingUser.MasterPasswordHint = MasterPasswordHint;
        existingUser.Kdf = MasterPasswordUnlock.Kdf.KdfType;
        existingUser.KdfIterations = MasterPasswordUnlock.Kdf.Iterations;
        existingUser.KdfMemory = MasterPasswordUnlock.Kdf.Memory;
        existingUser.KdfParallelism = MasterPasswordUnlock.Kdf.Parallelism;
        existingUser.Key = MasterPasswordUnlock.MasterKeyWrappedUserKey;
        existingUser.MasterPasswordSalt = MasterPasswordUnlock.Salt;

        Keys?.ToUser(existingUser);
        return existingUser;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // This endpoint has no TDE branch, so a request without a keypair would set the master password
        // and leave the user unconfirmable by an organization admin.
        if ((AccountKeys == null) == (Keys == null))
        {
            yield return new ValidationResult(
                $"Must specify exactly one of {nameof(AccountKeys)} or {nameof(Keys)}.",
                [nameof(AccountKeys), nameof(Keys)]);
        }

        // Validate KDF equality, salt equality, and KDF settings across MPAD and MPUD
        foreach (var validationResult in KdfSettingsValidator.ValidateAuthenticationAndUnlockData(
                     MasterPasswordAuthentication.ToData(), MasterPasswordUnlock.ToData()))
        {
            yield return validationResult;
        }
    }

    public SetInitialMasterPasswordDataModel ToData()
    {
        return new SetInitialMasterPasswordDataModel
        {
            MasterPasswordAuthentication = MasterPasswordAuthentication.ToData(),
            MasterPasswordUnlock = MasterPasswordUnlock.ToData(),
            OrgSsoIdentifier = OrgIdentifier,
            AccountKeys = AccountKeys?.ToAccountKeysData(),
            MasterPasswordHint = MasterPasswordHint
        };
    }
}
