using System.ComponentModel.DataAnnotations;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.Accounts;

public class TdeOnboardPasswordRequestModel : IValidatableObject
{
    public required MasterPasswordAuthenticationDataRequestModel MasterPasswordAuthentication { get; set; }
    public required MasterPasswordUnlockDataRequestModel MasterPasswordUnlock { get; set; }

    [StringLength(50)]
    public string? MasterPasswordHint { get; set; }

    [Required]
    public required string OrgIdentifier { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Validate Kdf
        var authenticationKdf = MasterPasswordAuthentication.Kdf.ToData();
        var unlockKdf = MasterPasswordUnlock.Kdf.ToData();

        // Currently, KDF settings are not saved separately for authentication and unlock and must therefore be equal
        if (!authenticationKdf.Equals(unlockKdf))
        {
            throw new BadRequestException("KDF settings must be equal for authentication and unlock.");
        }

        var authenticationValidationErrors = KdfSettingsValidator.Validate(authenticationKdf).ToList();
        if (authenticationValidationErrors.Count != 0)
        {
            yield return authenticationValidationErrors.First();
        }

        var unlockValidationErrors = KdfSettingsValidator.Validate(unlockKdf).ToList();
        if (unlockValidationErrors.Count != 0)
        {
            yield return unlockValidationErrors.First();
        }
    }

    public TdeOnboardMasterPasswordDataModel ToData()
    {
        return new TdeOnboardMasterPasswordDataModel
        {
            MasterPasswordAuthentication = MasterPasswordAuthentication.ToData(),
            MasterPasswordUnlock = MasterPasswordUnlock.ToData(),
            OrgSsoIdentifier = OrgIdentifier,
            MasterPasswordHint = MasterPasswordHint
        };
    }
}
