using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.Utilities;

public static class KdfSettingsValidator
{
    /// <summary>
    /// Validates that authentication and unlock data have matching KDF settings and salts,
    /// then validates the KDF settings themselves.
    /// </summary>
    public static IEnumerable<ValidationResult> ValidateAuthenticationAndUnlockData(
        MasterPasswordAuthenticationData authentication,
        MasterPasswordUnlockData unlock)
    {
        // Currently KDF settings are not saved separately for authentication and unlock and must therefore be equal
        if (!authentication.Kdf.Equals(unlock.Kdf))
        {
            yield return new ValidationResult(
                "KDF settings must be equal for authentication and unlock.",
                [nameof(authentication.Kdf)]);
            // KDF settings diverge; remaining validation is not meaningful
            yield break;
        }

        if (authentication.Salt != unlock.Salt)
        {
            yield return new ValidationResult(
                "Salt must be equal for authentication and unlock.",
                [nameof(authentication.Salt)]);
        }

        foreach (var validationResult in Validate(authentication.Kdf))
        {
            yield return validationResult;
        }
    }

    // PM-28143 - Remove below when fixing ticket
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

    public static IEnumerable<ValidationResult> Validate(KdfSettings settings)
    {
        return Validate(settings.KdfType, settings.Iterations, settings.Memory, settings.Parallelism);
    }
}
