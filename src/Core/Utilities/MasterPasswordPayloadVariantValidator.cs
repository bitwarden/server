using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Utilities;

public static class MasterPasswordPayloadVariantValidator
{
    /// <summary>
    /// Validates that exactly one variant of a master-password mutation payload is present:
    /// either the new shape (<c>AuthenticationData</c> + <c>UnlockData</c>) or the legacy shape
    /// (<c>NewMasterPasswordHash</c> + <c>Key</c>). To be removed alongside the legacy fields
    /// in PM-33141.
    /// </summary>
    public static IEnumerable<ValidationResult> ValidateExclusivity(bool hasNewPayloads, bool hasLegacyPayloads)
    {
        string[] memberNames =
        [
            "AuthenticationData",
            "UnlockData",
            "NewMasterPasswordHash",
            "Key"
        ];

        if (hasNewPayloads && hasLegacyPayloads)
        {
            yield return new ValidationResult(
                "Cannot provide both new payloads (UnlockData/AuthenticationData) and legacy payloads (NewMasterPasswordHash/Key).",
                memberNames);
        }

        if (!hasNewPayloads && !hasLegacyPayloads)
        {
            yield return new ValidationResult(
                "Must provide either new payloads (UnlockData/AuthenticationData) or legacy payloads (NewMasterPasswordHash/Key).",
                memberNames);
        }
    }
}
