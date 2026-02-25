using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationUserResetPasswordV2RequestModel : IValidatableObject
{
    [Required]
    public required MasterPasswordAuthenticationDataRequestModel MasterPasswordAuthentication { get; init; }

    [Required]
    public required MasterPasswordUnlockDataRequestModel MasterPasswordUnlock { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return KdfSettingsValidator.ValidateAuthenticationAndUnlockData(
            MasterPasswordAuthentication.ToData(),
            MasterPasswordUnlock.ToData());
    }
}
