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
    // TODO: legacy MasterPasswordHash, Key, and top-level KDF properties can be removed once client-side
    // changes in https://bitwarden.atlassian.net/browse/PM-35599 have been merged and have aged out according
    // to the Bitwarden release support policy. See comment on SetInitialPasswordV1Async() for further details.
    [Obsolete("Use MasterPasswordAuthentication instead")]
    [StringLength(300)]
    public string? MasterPasswordHash { get; set; }

    [Obsolete("Use MasterPasswordUnlock instead")]
    public string? Key { get; set; }

    [Obsolete("Use MasterPasswordUnlock instead")]
    public KdfType? Kdf { get; set; }

    [Obsolete("Use MasterPasswordUnlock instead")]
    public int? KdfIterations { get; set; }

    [Obsolete("Use MasterPasswordUnlock instead")]
    public int? KdfMemory { get; set; }

    [Obsolete("Use MasterPasswordUnlock instead")]
    public int? KdfParallelism { get; set; }

    // TODO: legacy Keys can be removed once the EnableAccountEncryptionV2JitPasswordRegistration feature flag
    // has been unwound in https://bitwarden.atlassian.net/browse/PM-27327. See comment on SetInitialPasswordV1Async()
    // for further details.
    [Obsolete("Use AccountKeys instead")]
    public KeysRequestModel? Keys { get; set; }

    public MasterPasswordAuthenticationDataRequestModel? MasterPasswordAuthentication { get; set; }
    public MasterPasswordUnlockDataRequestModel? MasterPasswordUnlock { get; set; }
    public AccountKeysRequestModel? AccountKeys { get; set; }

    [StringLength(50)]
    public string? MasterPasswordHint { get; set; }

    [Required]
    public required string OrgIdentifier { get; set; }

    // Reads KDF/key/salt from MasterPasswordUnlock when present (modern clients), and falls
    // back to the top-level legacy properties when not (older clients).
    //
    // TODO: Can be removed when its only consumer, SetInitialPasswordV1Async(), is removed. See comment
    // on that method for removal requirements.
    public User ToUser(User existingUser)
    {
        existingUser.MasterPasswordHint = MasterPasswordHint;
        existingUser.Kdf = MasterPasswordUnlock?.Kdf.KdfType ?? Kdf!.Value;
        existingUser.KdfIterations = MasterPasswordUnlock?.Kdf.Iterations ?? KdfIterations!.Value;
        existingUser.KdfMemory = MasterPasswordUnlock?.Kdf.Memory ?? KdfMemory;
        existingUser.KdfParallelism = MasterPasswordUnlock?.Kdf.Parallelism ?? KdfParallelism;
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
        if (AccountKeys != null && Keys != null)
        {
            yield return new ValidationResult(
                $"Cannot specify both {nameof(AccountKeys)} and {nameof(Keys)}. Provide exactly one keypair.",
                [nameof(AccountKeys), nameof(Keys)]);
        }

        if (HasAuthAndUnlockData())
        {
            // Validate KDF equality, salt equality, and KDF settings on the new-shape MPAD/MPUD fields
            foreach (var validationResult in KdfSettingsValidator.ValidateAuthenticationAndUnlockData(
                         MasterPasswordAuthentication!.ToData(), MasterPasswordUnlock!.ToData()))
            {
                yield return validationResult;
            }

            yield break;
        }

        // TODO: the following legacy shape validation can be removed once client-side changes in
        // https://bitwarden.atlassian.net/browse/PM-35599 have been merged and have aged out according
        // to the Bitwarden release support policy. See comment on SetInitialPasswordV1Async() for further details.
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
    /// True when the request uses the modern data shape (MasterPasswordAuthentication + MasterPasswordUnlock).
    /// This is a shape check, NOT a routing guarantee — modern-shape requests can route to:
    ///   - The TDE command, when no keypair is present (neither AccountKeys nor Keys)
    ///   - The V2 MP JIT command, when AccountKeys is present and the V2 MP JIT feature flag is on
    ///   - The V1 path, otherwise (e.g., modern V1 MP JIT with legacy Keys)
    /// See the `set-password` endpoint.
    /// </summary>
    public bool HasAuthAndUnlockData()
    {
        return MasterPasswordAuthentication != null && MasterPasswordUnlock != null;
    }

    /// <summary>
    /// True when the request does NOT contain a keypair. TDE users don't send a keypair on the
    /// request because they already have one. Checks both AccountKeys (new) and Keys (legacy) so
    /// the predicate is correct for the transitional period where clients may send either key shape.
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
