using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.KeyManagement.Models.Requests;

public class MasterPasswordUnlockAndAuthenticationDataModel : IValidatableObject
{
    public required KdfType KdfType { get; set; }
    public required int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }

    [StrictEmailAddress]
    [StringLength(256)]
    public required string Email { get; set; }
    [StringLength(300)]
    public required string MasterKeyAuthenticationHash { get; set; }
    [EncryptedString] public required string MasterKeyEncryptedUserKey { get; set; }
    [StringLength(50)]
    public string? MasterPasswordHint { get; set; }
    [MaxLength(256)]
    public string? MasterPasswordSalt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (KdfType == KdfType.PBKDF2_SHA256)
        {
            if (KdfMemory.HasValue || KdfParallelism.HasValue)
            {
                yield return new ValidationResult("KdfMemory and KdfParallelism must be null for PBKDF2_SHA256", [nameof(KdfMemory), nameof(KdfParallelism)]);
            }
        }
        else if (KdfType == KdfType.Argon2id)
        {
            if (!KdfMemory.HasValue || !KdfParallelism.HasValue)
            {
                yield return new ValidationResult("KdfMemory and KdfParallelism must have values for Argon2id", [nameof(KdfMemory), nameof(KdfParallelism)]);
            }
        }
        else
        {
            yield return new ValidationResult("Invalid KdfType", [nameof(KdfType)]);
        }
    }

    public MasterPasswordAuthenticationData ToAuthenticationData()
    {
        return new MasterPasswordAuthenticationData
        {
            Kdf = new KdfSettings
            {
                KdfType = KdfType,
                Iterations = KdfIterations,
                Memory = KdfMemory,
                Parallelism = KdfParallelism,
            },
            Salt = MasterPasswordSalt ?? Email,
            MasterPasswordAuthenticationHash = MasterKeyAuthenticationHash,
        };
    }

    public MasterPasswordUnlockData ToMasterPasswordUnlockData()
    {
        return new MasterPasswordUnlockData
        {
            Kdf = new KdfSettings
            {
                KdfType = KdfType,
                Iterations = KdfIterations,
                Memory = KdfMemory,
                Parallelism = KdfParallelism,
            },
            Salt = MasterPasswordSalt ?? Email,
            MasterKeyWrappedUserKey = MasterKeyEncryptedUserKey,
        };
    }
}
