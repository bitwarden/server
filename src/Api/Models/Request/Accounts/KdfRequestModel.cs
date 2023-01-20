using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Api.Models.Request.Accounts;

public class KdfRequestModel : PasswordRequestModel, IValidatableObject
{
    [Required]
    public KdfType? Kdf { get; set; }
    [Required]
    public int? KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Kdf.HasValue && KdfIterations.HasValue)
        {
            switch (Kdf.Value)
            {
                case KdfType.PBKDF2_SHA256:
                    if (KdfIterations.Value < 5000 || KdfIterations.Value > 2_000_000)
                    {
                        yield return new ValidationResult("KDF iterations must be between 5000 and 2000000.");
                    }
                    break;
                case KdfType.Argon2id:
                    if (!KdfIterations.HasValue || !(KdfIterations.Value > 0))
                    {
                        yield return new ValidationResult("Argon2 iterations must be greater than 0");
                    }
                    else if (!KdfMemory.HasValue || KdfMemory.Value < 15 || KdfMemory.Value > 1024)
                    {
                        yield return new ValidationResult("Argon2 memory must be between 15MiB and 1GiB");
                    } else if (!KdfParallelism.HasValue || KdfParallelism.Value < 1 || KdfParallelism.Value > 16)
                    {
                        yield return new ValidationResult("Argon2 parallelism must be between 1 and 16");
                    }
                    break;

                default:
                    break;
            }
        }
    }
}
