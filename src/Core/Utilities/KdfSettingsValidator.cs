using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;

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

    public static IEnumerable<ValidationResult> Validate(MasterPasswordUnlockData masterPasswordUnlockData)
    {
        switch (masterPasswordUnlockData.Kdf.KdfType)
        {
            case KdfType.PBKDF2_SHA256:
                if (!AuthConstants.PBKDF2_ITERATIONS.InsideRange(masterPasswordUnlockData.Kdf.Iterations))
                {
                    yield return new ValidationResult($"KDF iterations must be between {AuthConstants.PBKDF2_ITERATIONS.Min} and {AuthConstants.PBKDF2_ITERATIONS.Max}.");
                }
                break;
            case KdfType.Argon2id:
                if (!AuthConstants.ARGON2_ITERATIONS.InsideRange(masterPasswordUnlockData.Kdf.Iterations))
                {
                    yield return new ValidationResult($"Argon2 iterations must be between {AuthConstants.ARGON2_ITERATIONS.Min} and {AuthConstants.ARGON2_ITERATIONS.Max}.");
                }
                else if (!masterPasswordUnlockData.Kdf.Memory.HasValue || !AuthConstants.ARGON2_MEMORY.InsideRange(masterPasswordUnlockData.Kdf.Memory.Value))
                {
                    yield return new ValidationResult($"Argon2 memory must be between {AuthConstants.ARGON2_MEMORY.Min}mb and {AuthConstants.ARGON2_MEMORY.Max}mb.");
                }
                else if (!masterPasswordUnlockData.Kdf.Parallelism.HasValue || !AuthConstants.ARGON2_PARALLELISM.InsideRange(masterPasswordUnlockData.Kdf.Parallelism.Value))
                {
                    yield return new ValidationResult($"Argon2 parallelism must be between {AuthConstants.ARGON2_PARALLELISM.Min} and {AuthConstants.ARGON2_PARALLELISM.Max}.");
                }
                break;
        }
    }

    public static IEnumerable<ValidationResult> Validate(KdfSettings settings)
    {
        return Validate(settings.KdfType, settings.Iterations, settings.Memory, settings.Parallelism);
    }
}
