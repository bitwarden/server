using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api.Request.Accounts;

namespace Bit.Api.Models.Request.Accounts;

public class SetPasswordRequestModel : IValidatableObject
{
    [Required]
    [StringLength(300)]
    public string MasterPasswordHash { get; set; }
    [Required]
    public string Key { get; set; }
    [StringLength(50)]
    public string MasterPasswordHint { get; set; }
    [Required]
    public KeysRequestModel Keys { get; set; }
    [Required]
    public KdfType Kdf { get; set; }
    [Required]
    public int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }
    public string OrgIdentifier { get; set; }

    public User ToUser(User existingUser)
    {
        existingUser.MasterPasswordHint = MasterPasswordHint;
        existingUser.Kdf = Kdf;
        existingUser.KdfIterations = KdfIterations;
        existingUser.KdfMemory = KdfMemory;
        existingUser.KdfParallelism = KdfParallelism;
        existingUser.Key = Key;
        Keys.ToUser(existingUser);
        return existingUser;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        switch (Kdf)
        {
            case KdfType.PBKDF2_SHA256:
                if (KdfIterations < 5000 || KdfIterations > 2_000_000)
                {
                    yield return new ValidationResult("KDF iterations must be between 5000 and 2000000.");
                }
                break;
            case KdfType.Argon2id:
                if (KdfIterations < 0)
                {
                    yield return new ValidationResult("Argon2 iterations must be greater than 0.");
                }
                else if (!KdfMemory.HasValue || KdfMemory.Value < 15 || KdfMemory.Value > 1024)
                {
                    yield return new ValidationResult("Argon2 memory must be between 15mb and 1024mb.");
                }
                else if (!KdfParallelism.HasValue || KdfParallelism.Value < 1 || KdfParallelism.Value > 16)
                {
                    yield return new ValidationResult("Argon2 parallelism must be between 1 and 16.");
                }
                break;

            default:
                break;
        }
    }
}
