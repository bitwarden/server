using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.Utilities;

public static class KdfSettingsValidator
{
    /// <summary>
    /// Validates that authentication and unlock data have matching KDF settings and salts,
    /// then validates the KDF settings themselves. This should be used when setting
    /// the KDF settings. This should NOT be used when merely changing settings affected
    /// by the kdf settings (email-salt, password, key-rotation) — for those flows, use
    /// <see cref="ValidateKdfAndSaltAgreement"/> instead.
    /// </summary>
    public static IEnumerable<ValidationResult> ValidateAuthenticationAndUnlockData(
        MasterPasswordAuthenticationData authentication,
        MasterPasswordUnlockData unlock)
    {
        if (string.IsNullOrWhiteSpace(authentication.Salt))
        {
            yield return new ValidationResult(
                "Master password salt must not be empty.",
                [nameof(authentication.Salt)]);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(unlock.Salt))
        {
            yield return new ValidationResult(
                "Master password salt must not be empty.",
                [nameof(unlock.Salt)]);
            yield break;
        }

        // Currently KDF settings are not saved separately for authentication and unlock and must therefore be equal
        if (!authentication.Kdf.Equals(unlock.Kdf))
        {
            yield return new ValidationResult(
                "AuthenticationData and UnlockData must have the same KDF configuration.",
                [nameof(authentication.Kdf)]);
            // KDF settings diverge; remaining validation is not meaningful
            yield break;
        }

        // Salt must be equal for authentication and unlock to prevent de-synced salt value
        if (authentication.Salt != unlock.Salt)
        {
            yield return new ValidationResult(
                "Invalid master password salt.",
                [nameof(authentication.Salt)]);
        }

        foreach (var validationResult in Validate(authentication.Kdf))
        {
            yield return validationResult;
        }
    }

    /// <summary>
    /// Validates that authentication and unlock data have matching KDF settings and salts,
    /// without validating that the KDF settings themselves fall within current range. 
    ///
    /// Use when: the flow's downstream contract requires the inbound KDF to match the
    /// user's stored KDF unchanged (e.g., flows backed by
    /// <see cref="Auth.UserFeatures.UserMasterPassword.Data.UpdateExistingPasswordData"/>).
    ///
    /// For flows where the KDF is being set or changed (registration, KDF rotation,
    /// initial password set, TDE offboarding), use
    /// <see cref="ValidateAuthenticationAndUnlockData"/> instead — those flows require range
    /// enforcement.
    /// </summary>
    public static IEnumerable<ValidationResult> ValidateKdfAndSaltAgreement(
        MasterPasswordAuthenticationData authentication,
        MasterPasswordUnlockData unlock)
    {
        if (string.IsNullOrWhiteSpace(authentication.Salt))
        {
            yield return new ValidationResult(
                "Master password salt must not be empty.",
                [nameof(authentication.Salt)]);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(unlock.Salt))
        {
            yield return new ValidationResult(
                "Master password salt must not be empty.",
                [nameof(unlock.Salt)]);
            yield break;
        }

        if (!authentication.Kdf.Equals(unlock.Kdf))
        {
            yield return new ValidationResult(
                "AuthenticationData and UnlockData must have the same KDF configuration.",
                [nameof(authentication.Kdf)]);
            yield break;
        }

        if (authentication.Salt != unlock.Salt)
        {
            yield return new ValidationResult(
                "Invalid master password salt.",
                [nameof(authentication.Salt)]);
        }
    }

    // PM-28143 - Remove below when fixing ticket
    public static IEnumerable<ValidationResult> Validate(KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        switch (kdfType)
        {
            case KdfType.PBKDF2_SHA256:
                if (!KdfConstants.PBKDF2_ITERATIONS.InsideRange(kdfIterations))
                {
                    yield return new ValidationResult($"KDF iterations must be between {KdfConstants.PBKDF2_ITERATIONS.Min} and {KdfConstants.PBKDF2_ITERATIONS.Max}.");
                }
                break;
            case KdfType.Argon2id:
                if (!KdfConstants.ARGON2_ITERATIONS.InsideRange(kdfIterations))
                {
                    yield return new ValidationResult($"Argon2 iterations must be between {KdfConstants.ARGON2_ITERATIONS.Min} and {KdfConstants.ARGON2_ITERATIONS.Max}.");
                }
                else if (!kdfMemory.HasValue || !KdfConstants.ARGON2_MEMORY.InsideRange(kdfMemory.Value))
                {
                    yield return new ValidationResult($"Argon2 memory must be between {KdfConstants.ARGON2_MEMORY.Min}mb and {KdfConstants.ARGON2_MEMORY.Max}mb.");
                }
                else if (!kdfParallelism.HasValue || !KdfConstants.ARGON2_PARALLELISM.InsideRange(kdfParallelism.Value))
                {
                    yield return new ValidationResult($"Argon2 parallelism must be between {KdfConstants.ARGON2_PARALLELISM.Min} and {KdfConstants.ARGON2_PARALLELISM.Max}.");
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
