using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.Accounts;

/// <summary>
/// Request model for changing the KDF settings for a user's account.
/// </summary>
public class ChangeKdfRequestModel : IValidatableObject
{
    // The current master password hash; proves user has access to the MP
    [Required]
    public required string MasterPasswordHash { get; set; }

    [Required]
    public required MasterPasswordAuthenticationDataRequestModel AuthenticationData { get; set; }
    [Required]
    public required MasterPasswordUnlockDataRequestModel UnlockData { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // [Required] on AuthenticationData/UnlockData reports null-field errors via ModelState;
        // this method only runs cross-field validation when both are present.
        if (AuthenticationData == null || UnlockData == null)
        {
            yield break;
        }

        foreach (var validationResult in KdfSettingsValidator.ValidateAuthenticationAndUnlockData(
                     AuthenticationData.ToData(), UnlockData.ToData()))
        {
            yield return validationResult;
        }
    }
}
