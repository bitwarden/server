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
    // TODO: removal requires that BOTH flags have been removed:
    //  - https://bitwarden.atlassian.net/browse/PM-27327 (MP)
    //  - https://bitwarden.atlassian.net/browse/PM-27329 (TDE)
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

    // Reads KDF/key from MasterPasswordAuthentication/MasterPasswordUnlock when present (modern clients),
    // and falls back to the top-level legacy properties when not (older clients).
    // TODO: removal requires that BOTH flags have been removed:
    //  - https://bitwarden.atlassian.net/browse/PM-27327 (MP)
    //  - https://bitwarden.atlassian.net/browse/PM-27329 (TDE)
    public User ToUser(User existingUser)
    {
        existingUser.MasterPasswordHint = MasterPasswordHint;
        existingUser.Kdf = MasterPasswordAuthentication?.Kdf.KdfType ?? Kdf!.Value;
        existingUser.KdfIterations = MasterPasswordAuthentication?.Kdf.Iterations ?? KdfIterations!.Value;
        existingUser.KdfMemory = MasterPasswordAuthentication?.Kdf.Memory ?? KdfMemory;
        existingUser.KdfParallelism = MasterPasswordAuthentication?.Kdf.Parallelism ?? KdfParallelism;
        existingUser.Key = MasterPasswordUnlock?.MasterKeyWrappedUserKey ?? Key;

        // MasterPasswordSalt column must never be null/empty after a successful password-set
        // operation. Modern clients send an explicit salt via MPUD; older clients don't send one,
        // so we fall back to the email-derived V1 salt (matching the implicit contract that
        // User.GetMasterPasswordSalt() already encodes at read time).
        existingUser.MasterPasswordSalt = MasterPasswordUnlock?.Salt ?? existingUser.Email.ToLowerInvariant().Trim();

        Keys?.ToUser(existingUser);
        return existingUser;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (HasAuthAndUnlockData())
        {
            // V2 registration - validate KDF equality, salt equality, and KDF settings
            foreach (var validationResult in KdfSettingsValidator.ValidateAuthenticationAndUnlockData(
                         MasterPasswordAuthentication!.ToData(), MasterPasswordUnlock!.ToData()))
            {
                yield return validationResult;
            }

            yield break;
        }

        // V1 registration
        // TODO: removal requires that BOTH flags have been removed:
        //  - https://bitwarden.atlassian.net/browse/PM-27327 (MP)
        //  - https://bitwarden.atlassian.net/browse/PM-27329 (TDE)
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

    /// <summary>
    /// True when the request uses the new data shape (MasterPasswordAuthentication + MasterPasswordUnlock).
    /// This is a shape check, NOT a guarantee that V2 encryption will run. It is possible for V1 encryption
    /// to run even when the request contains these new data types (see `set-password` endpoint).
    /// Feature flags and AccountKeys presence determine the actual flow (V1 or V2).
    /// </summary>
    public bool HasAuthAndUnlockData()
    {
        return MasterPasswordAuthentication != null && MasterPasswordUnlock != null;
    }

    /// <summary>
    /// True when the request comes from a TDE user (TDE users don't send a keypair because
    /// their keypair already exists). Checks both AccountKeys (new) and Keys (legacy) so the
    /// predicate is correct for the transitional period where clients may send either key shape.
    /// </summary>
    public bool IsTdeSetPasswordRequest()
    {
        return AccountKeys == null && Keys == null;
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
