using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Api.Request;

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

        // Either both must be present or none should be present
        if ((AuthenticationData == null) != (UnlockData == null))
        {
            yield return new ValidationResult(
                "AuthenticationData and UnlockData must be provided.",
                [nameof(AuthenticationData), nameof(UnlockData)]);
        }
        if (AuthenticationData != null && UnlockData != null)
        {
            if (!AuthenticationData.HasSameKdfConfiguration(UnlockData))
            {
                yield return new ValidationResult(
                    "AuthenticationData and UnlockData must have the same KDF configuration.",
                    [nameof(AuthenticationData), nameof(UnlockData)]);
            }

            if (!AuthenticationData.Salt.Equals(UnlockData.Salt))
            {
                yield return new ValidationResult(
                    "AuthenticationData and UnlockData must have the same salt.",
                    [nameof(AuthenticationData), nameof(UnlockData)]);
            }
        }
    }
}
