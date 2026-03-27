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
        foreach (var result in base.Validate(validationContext))
        {
            yield return result;
        }

        // Enforce both-or-neither for authentication and unlock data
        if (AuthenticationData != null && UnlockData == null)
        {
            yield return new ValidationResult(
                $"{nameof(UnlockData)} must be provided when {nameof(AuthenticationData)} is provided.",
                [nameof(UnlockData)]);
            yield break;
        }

        if (AuthenticationData == null && UnlockData != null)
        {
            yield return new ValidationResult(
                $"{nameof(AuthenticationData)} must be provided when {nameof(UnlockData)} is provided.",
                [nameof(AuthenticationData)]);
            yield break;
        }

        // Validate KDF equality, salt equality, and KDF settings when both are present
        if (AuthenticationData != null && UnlockData != null)
        {
            foreach (var result in KdfSettingsValidator.ValidateAuthenticationAndUnlockData(
                         AuthenticationData.ToData(), UnlockData.ToData()))
            {
                yield return result;
            }
        }
    }
}
