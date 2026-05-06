using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class PasswordRequestModel : IValidatableObject
{
    [Required]
    public required string MasterPasswordHash { get; set; }
    [Obsolete("To be removed in PM-33141")]
    [StringLength(300)]
    public string? NewMasterPasswordHash { get; set; }
    [Obsolete("To be removed in PM-33141")]
    public string? Key { get; set; }
    [StringLength(50)]
    public string? MasterPasswordHint { get; set; }

    // Should be made required in PM-33141
    public MasterPasswordAuthenticationDataRequestModel? AuthenticationData { get; set; }
    // Should be made required in PM-33141
    public MasterPasswordUnlockDataRequestModel? UnlockData { get; set; }

    public bool RequestHasNewDataTypes()
    {
        return UnlockData is not null && AuthenticationData is not null;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasLegacyPayloads = NewMasterPasswordHash is not null && Key is not null;

        foreach (var validationResult in MasterPasswordPayloadVariantValidator.ValidateExclusivity(
                     RequestHasNewDataTypes(), hasLegacyPayloads))
        {
            yield return validationResult;
        }

        if (RequestHasNewDataTypes())
        {
            if (!AuthenticationData!.HasSameKdfConfiguration(UnlockData!))
            {
                yield return new ValidationResult(
                    "AuthenticationData and UnlockData must have the same KDF configuration.",
                    [nameof(AuthenticationData), nameof(UnlockData)]);
            }
            if (!AuthenticationData.Salt.Equals(UnlockData!.Salt))
            {
                yield return new ValidationResult(
                    "AuthenticationData and UnlockData must have the same salt.",
                    [nameof(AuthenticationData), nameof(UnlockData)]);
            }
        }
    }
}
