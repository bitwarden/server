using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.KeyManagement.Models.Api.Request;

public class SetKeyConnectorKeyRequestModel : IValidatableObject
{
    // TODO will be removed with https://bitwarden.atlassian.net/browse/PM-27328
    [Obsolete("Use KeyConnectorKeyWrappedUserKey instead")]
    public string? Key { get; set; }

    [Obsolete("Use AccountKeys instead")]
    public KeysRequestModel? Keys { get; set; }
    [Obsolete("Not used anymore")]
    public KdfType? Kdf { get; set; }
    [Obsolete("Not used anymore")]
    public int? KdfIterations { get; set; }
    [Obsolete("Not used anymore")]
    public int? KdfMemory { get; set; }
    [Obsolete("Not used anymore")]
    public int? KdfParallelism { get; set; }

    [EncryptedString]
    public string? KeyConnectorKeyWrappedUserKey { get; set; }
    public AccountKeysRequestModel? AccountKeys { get; set; }

    [Required]
    public required string OrgIdentifier { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsV2Request())
        {
            // V2 registration
            yield break;
        }

        // V1 registration
        // TODO removed with https://bitwarden.atlassian.net/browse/PM-27328
        if (string.IsNullOrEmpty(Key))
        {
            yield return new ValidationResult("Key must be supplied.");
        }

        if (Keys == null)
        {
            yield return new ValidationResult("Keys must be supplied.");
        }

        if (Kdf == null)
        {
            yield return new ValidationResult("Kdf must be supplied.");
        }

        if (KdfIterations == null)
        {
            yield return new ValidationResult("KdfIterations must be supplied.");
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
    }

    public bool IsV2Request()
    {
        return !string.IsNullOrEmpty(KeyConnectorKeyWrappedUserKey) && AccountKeys != null;
    }

    // TODO removed with https://bitwarden.atlassian.net/browse/PM-27328
    public User ToUser(User existingUser)
    {
        existingUser.Kdf = Kdf!.Value;
        existingUser.KdfIterations = KdfIterations!.Value;
        existingUser.KdfMemory = KdfMemory;
        existingUser.KdfParallelism = KdfParallelism;
        existingUser.Key = Key;
        Keys!.ToUser(existingUser);
        return existingUser;
    }
}
