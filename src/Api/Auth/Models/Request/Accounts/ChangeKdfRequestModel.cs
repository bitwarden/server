using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class ChangeKdfRequestModel : IValidatableObject
{
    [Required]
    public required string MasterPasswordHash { get; set; }
    [Obsolete("To be removed in PM-33141")]
    [StringLength(300)]
    public string? NewMasterPasswordHash { get; set; }
    [Obsolete("To be removed in PM-33141")]
    public string? Key { get; set; }

    // Should be made required in PM-33141
    public MasterPasswordAuthenticationDataRequestModel? AuthenticationData { get; set; }
    // Should be made required in PM-33141
    public MasterPasswordUnlockDataRequestModel? UnlockData { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasNewPayloads = AuthenticationData is not null && UnlockData is not null;
        var hasLegacyPayloads = NewMasterPasswordHash is not null && Key is not null;

        if (hasNewPayloads && !hasLegacyPayloads)
        {
            foreach (var validationResult in KdfSettingsValidator.ValidateAuthenticationAndUnlockData(
                         AuthenticationData!.ToData(), UnlockData!.ToData()))
            {
                yield return validationResult;
            }
        }

        if (hasNewPayloads && hasLegacyPayloads)
        {
            yield return new ValidationResult(
                "Cannot provide both new payloads (UnlockData/AuthenticationData) and legacy payloads (NewMasterPasswordHash/Key).",
                [nameof(AuthenticationData), nameof(UnlockData), nameof(NewMasterPasswordHash), nameof(Key)]);
        }

        if (!hasNewPayloads && !hasLegacyPayloads)
        {
            yield return new ValidationResult(
                "Must provide either new payloads (UnlockData/AuthenticationData) or legacy payloads (NewMasterPasswordHash/Key).",
                [nameof(AuthenticationData), nameof(UnlockData), nameof(NewMasterPasswordHash), nameof(Key)]);
        }
    }
}
