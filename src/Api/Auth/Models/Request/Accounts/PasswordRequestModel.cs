using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;


namespace Bit.Api.Auth.Models.Request.Accounts;

public class PasswordRequestModel : SecretVerificationRequestModel
{
    [Required]
    [StringLength(300)]
    public required string NewMasterPasswordHash { get; set; }
    [StringLength(50)]
    public string? MasterPasswordHint { get; set; }
    [Required]
    public required string Key { get; set; }

    // Note: These will eventually become required, but not all consumers are moved over yet.
    public MasterPasswordAuthenticationDataRequestModel? AuthenticationData { get; set; }
    public MasterPasswordUnlockDataRequestModel? UnlockData { get; set; }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // validate the secrets for the base class first
        foreach (var result in base.Validate(validationContext))
        {
            yield return result;
        }

        // Enforce: if one is provided, both must be provided.
        if (AuthenticationData != null && UnlockData != null)
        {
            foreach (var validationResult in KdfSettingsValidator.ValidateAuthenticationAndUnlockData(
                AuthenticationData.ToData(), UnlockData.ToData()))
            {
                yield return validationResult;
            }
        }
        else
        {
            yield return new ValidationResult(
                "AuthenticationData and UnlockData must be provided.",
                [nameof(AuthenticationData), nameof(UnlockData)]);
        }
    }
}
