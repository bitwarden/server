using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Api.Request;

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

    public MasterPasswordAuthenticationDataRequestModel? AuthenticationData { get; set; }
    public MasterPasswordUnlockDataRequestModel? UnlockData { get; set; }

    // To be removed in PM-33141
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasNewPayloads = AuthenticationData is not null && UnlockData is not null;
        var hasLegacyPayloads = NewMasterPasswordHash is not null && Key is not null;

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
