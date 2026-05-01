using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class UpdateTempPasswordRequestModel : IValidatableObject
{
    [Obsolete("To be removed in PM-33141")]
    [StringLength(300)]
    public string? NewMasterPasswordHash { get; set; }
    [Obsolete("To be removed in PM-33141")]
    public string? Key { get; set; }
    [StringLength(50)]
    public string? MasterPasswordHint { get; set; }

    // Should be made required in PM-33141
    public MasterPasswordUnlockDataRequestModel? UnlockData { get; set; }
    // Should be made required in PM-33141
    public MasterPasswordAuthenticationDataRequestModel? AuthenticationData { get; set; }

    public bool RequestHasNewDataTypes()
    {
        return UnlockData is not null && AuthenticationData is not null;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasNewPayloads = UnlockData is not null && AuthenticationData is not null;
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
                [nameof(UnlockData), nameof(AuthenticationData), nameof(NewMasterPasswordHash), nameof(Key)]);
        }

        if (!hasNewPayloads && !hasLegacyPayloads)
        {
            yield return new ValidationResult(
                "Must provide either new payloads (UnlockData/AuthenticationData) or legacy payloads (NewMasterPasswordHash/Key).",
                [nameof(UnlockData), nameof(AuthenticationData), nameof(NewMasterPasswordHash), nameof(Key)]);
        }
    }
}
