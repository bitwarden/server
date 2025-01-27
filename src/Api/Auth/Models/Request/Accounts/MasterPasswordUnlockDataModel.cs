using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class MasterPasswordUnlockDataModel : IValidatableObject
{
    [Required]
    public KdfType KdfType { get; set; }
    [Required]
    public int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }

    [Required]
    public string Email { get; set; }
    [Required]
    [StringLength(300)]
    public string MasterKeyAuthenticationHash { get; set; }
    [Required]
    public string MasterKeyEncryptedUserKey { get; set; }
    [StringLength(50)]
    public string MasterPasswordHint { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (KdfType == KdfType.PBKDF2_SHA256)
        {
            if (KdfMemory.HasValue || KdfParallelism.HasValue)
            {
                yield return new ValidationResult("KdfMemory and KdfParallelism must be null for PBKDF2_SHA256", new[] { nameof(KdfMemory), nameof(KdfParallelism) });
            }
        }
        else if (KdfType == KdfType.Argon2id)
        {
            if (!KdfMemory.HasValue || !KdfParallelism.HasValue)
            {
                yield return new ValidationResult("KdfMemory and KdfParallelism must have values for Argon2id", new[] { nameof(KdfMemory), nameof(KdfParallelism) });
            }
        }
        else
        {
            yield return new ValidationResult("Invalid KdfType", new[] { nameof(KdfType) });
        }
    }

    public MasterPasswordUnlockData ToUnlockData()
    {
        var data = new MasterPasswordUnlockData
        {
            KdfType = KdfType,
            KdfIterations = KdfIterations,
            KdfMemory = KdfMemory,
            KdfParallelism = KdfParallelism,

            Email = Email,

            MasterKeyAuthenticationHash = MasterKeyAuthenticationHash,
            MasterKeyEncryptedUserKey = MasterKeyEncryptedUserKey,
            MasterPasswordHint = MasterPasswordHint
        };
        return data;
    }

}
