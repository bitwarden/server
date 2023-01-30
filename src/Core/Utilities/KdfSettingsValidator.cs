using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;

namespace Bit.Core.Utilities;

public static class KdfSettingsValidator
{
    public static IEnumerable<ValidationResult> Validate(KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        switch (kdfType)
        {
            case KdfType.PBKDF2_SHA256:
                if (kdfIterations < 5000 || kdfIterations > 2_000_000)
                {
                    yield return new ValidationResult("KDF iterations must be between 5000 and 2000000.");
                }
                break;
            case KdfType.Argon2id:
                if (kdfIterations <= 0)
                {
                    yield return new ValidationResult("Argon2 iterations must be greater than 0.");
                }
                else if (!kdfMemory.HasValue || kdfMemory.Value < 15 || kdfMemory.Value > 1024)
                {
                    yield return new ValidationResult("Argon2 memory must be between 15mb and 1024mb.");
                }
                else if (!kdfParallelism.HasValue || kdfParallelism.Value < 1 || kdfParallelism.Value > 16)
                {
                    yield return new ValidationResult("Argon2 parallelism must be between 1 and 16.");
                }
                break;

            default:
                break;
        }
    }
}
