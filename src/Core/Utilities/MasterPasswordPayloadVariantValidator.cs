using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Utilities;

public static class MasterPasswordPayloadVariantValidator
{
    /// <summary>
    /// Validates that at least one variant of a master-password mutation payload is present:
    /// either the new shape (<c>AuthenticationData</c> + <c>UnlockData</c>) or the legacy shape
    /// (<c>NewMasterPasswordHash</c> + <c>Key</c>), or both. During the transition period,
    /// clients may send both; callers prefer the new shape when present.
    /// To be removed alongside the legacy fields in PM-33141.
    /// </summary>
    public static IEnumerable<ValidationResult> ValidateExclusivity(bool hasNewPayloads, bool hasLegacyPayloads)
    {
        if (!hasNewPayloads && !hasLegacyPayloads)
        {
            yield return new ValidationResult(
                "Must provide either new payloads (UnlockData/AuthenticationData) or legacy payloads (NewMasterPasswordHash/Key).",
                [
                    "AuthenticationData",
                    "UnlockData",
                    "NewMasterPasswordHash",
                    "Key"
                ]);
        }
    }
}
