using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class SetInitialPasswordRequestModel : IValidatableObject
{
    // TODO will be removed with https://bitwarden.atlassian.net/browse/PM-27327
    [Obsolete("Use MasterPasswordAuthentication instead")]
    [StringLength(300)]
    public string? MasterPasswordHash { get; set; }

    [Obsolete("Use MasterPasswordUnlock instead")]
    public string? Key { get; set; }

    [Obsolete("Use AccountKeys instead")]
    public KeysRequestModel? Keys { get; set; }

    [Obsolete("Use MasterPasswordAuthentication instead")]
    public KdfType? Kdf { get; set; }

    [Obsolete("Use MasterPasswordAuthentication instead")]
    public int? KdfIterations { get; set; }

    [Obsolete("Use MasterPasswordAuthentication instead")]
    public int? KdfMemory { get; set; }

    [Obsolete("Use MasterPasswordAuthentication instead")]
    public int? KdfParallelism { get; set; }

    public MasterPasswordAuthenticationDataRequestModel? MasterPasswordAuthentication { get; set; }
    public MasterPasswordUnlockDataRequestModel? MasterPasswordUnlock { get; set; }
    public AccountKeysRequestModel? AccountKeys { get; set; }

    [StringLength(50)]
    public string? MasterPasswordHint { get; set; }

    [Required]
    public required string OrgIdentifier { get; set; }

    // TODO removed with https://bitwarden.atlassian.net/browse/PM-27327
    public User ToUser(User existingUser)
    {
        existingUser.MasterPasswordHint = MasterPasswordHint;
        existingUser.Kdf = Kdf!.Value;
        existingUser.KdfIterations = KdfIterations!.Value;
        existingUser.KdfMemory = KdfMemory;
        existingUser.KdfParallelism = KdfParallelism;
        existingUser.Key = Key;
        Keys?.ToUser(existingUser);
        return existingUser;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsV2Request())
        {
            // V2 registration

            // Validate Kdf
            var authenticationKdf = MasterPasswordAuthentication!.Kdf.ToData();
            var unlockKdf = MasterPasswordUnlock!.Kdf.ToData();

            // Currently, KDF settings are not saved separately for authentication and unlock and must therefore be equal
            if (!authenticationKdf.Equals(unlockKdf))
            {
                yield return new ValidationResult("KDF settings must be equal for authentication and unlock.",
                    [$"{nameof(MasterPasswordAuthentication)}.{nameof(MasterPasswordAuthenticationDataRequestModel.Kdf)}",
                        $"{nameof(MasterPasswordUnlock)}.{nameof(MasterPasswordUnlockDataRequestModel.Kdf)}"]);
            }

            var authenticationValidationErrors = KdfSettingsValidator.Validate(authenticationKdf).ToList();
            if (authenticationValidationErrors.Count != 0)
            {
                yield return authenticationValidationErrors.First();
            }

            var unlockValidationErrors = KdfSettingsValidator.Validate(unlockKdf).ToList();
            if (unlockValidationErrors.Count != 0)
            {
                yield return unlockValidationErrors.First();
            }

            yield break;
        }

        // V1 registration
        // TODO removed with https://bitwarden.atlassian.net/browse/PM-27327
        if (string.IsNullOrEmpty(MasterPasswordHash))
        {
            yield return new ValidationResult("MasterPasswordHash must be supplied.");
        }

        if (string.IsNullOrEmpty(Key))
        {
            yield return new ValidationResult("Key must be supplied.");
        }

        if (Kdf == null)
        {
            yield return new ValidationResult("Kdf must be supplied.");
            yield break;
        }

        if (KdfIterations == null)
        {
            yield return new ValidationResult("KdfIterations must be supplied.");
            yield break;
        }

        if (Kdf == KdfType.Argon2id)
        {
            if (KdfMemory == null)
            {
                yield return new ValidationResult("KdfMemory must be supplied when Kdf is Argon2id.");
            }

            if (KdfParallelism == null)
            {
                yield return new ValidationResult("KdfParallelism must be supplied when Kdf is Argon2id.");
            }
        }

        var validationErrors = KdfSettingsValidator
            .Validate(Kdf!.Value, KdfIterations!.Value, KdfMemory, KdfParallelism).ToList();
        if (validationErrors.Count != 0)
        {
            yield return validationErrors.First();
        }
    }

    public bool IsV2Request()
    {
        // AccountKeys can be null for TDE users, so we don't check that here
        return MasterPasswordAuthentication != null && MasterPasswordUnlock != null;
    }

    public bool IsTdeSetPasswordRequest()
    {
        return AccountKeys == null;
    }

    public SetInitialMasterPasswordDataModel ToData()
    {
        return new SetInitialMasterPasswordDataModel
        {
            MasterPasswordAuthentication = MasterPasswordAuthentication!.ToData(),
            MasterPasswordUnlock = MasterPasswordUnlock!.ToData(),
            OrgSsoIdentifier = OrgIdentifier,
            AccountKeys = AccountKeys?.ToAccountKeysData(),
            MasterPasswordHint = MasterPasswordHint
        };
    }
}
