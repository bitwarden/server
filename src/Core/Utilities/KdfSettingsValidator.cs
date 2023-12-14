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
                if (!AuthConstants.PBKDF2_ITERATIONS.InsideRange(kdfIterations))
                {
                    yield return new ValidationResult($"KDF iterations must be between {AuthConstants.PBKDF2_ITERATIONS.Min} and {AuthConstants.PBKDF2_ITERATIONS.Max}.");
                }
                break;
            case KdfType.Argon2id:
                if (!AuthConstants.ARGON2_ITERATIONS.InsideRange(kdfIterations))
                {
                    yield return new ValidationResult($"Argon2 iterations must be between {AuthConstants.ARGON2_ITERATIONS.Min} and {AuthConstants.ARGON2_ITERATIONS.Max}.");
                }
                else if (!kdfMemory.HasValue || !AuthConstants.ARGON2_MEMORY.InsideRange(kdfMemory.Value))
                {
                    yield return new ValidationResult($"Argon2 memory must be between {AuthConstants.ARGON2_MEMORY.Min}mb and {AuthConstants.ARGON2_MEMORY.Max}mb.");
                }
                else if (!kdfParallelism.HasValue || !AuthConstants.ARGON2_PARALLELISM.InsideRange(kdfParallelism.Value))
                {
                    yield return new ValidationResult($"Argon2 parallelism must be between {AuthConstants.ARGON2_PARALLELISM.Min} and {AuthConstants.ARGON2_PARALLELISM.Max}.");
                }
                break;

            default:
                break;
        }
    }
}
